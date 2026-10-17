"""Trace shipped body and motion data to inspected, byte-preserved CC0 ZIP members.

This verifies asset identity and consumption, not visual quality or gameplay.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
BASE = 'Assets/ThirdParty/Quaternius/'
INSPECTION = BASE + 'archive-inspection.json'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load(path):
    return json.loads((ROOT / path).read_text())


def geometry_errors(mesh):
    """Validate baked geometry independently of its recorded content hash."""
    vertices, triangles = mesh['vertices'], mesh['triangles']
    errors = []
    if not vertices or not triangles or len(triangles) % 3:
        errors.append('Empty/incomplete body geometry')
    if any(type(i) is not int or not 0 <= i < len(vertices) for i in triangles):
        errors.append('Invalid body triangle index')
    for v in vertices:
        if not all(math.isfinite(v[k]) for k in ('x', 'y', 'z', 'r', 'g', 'b')):
            errors.append('Nonfinite body vertex')
            break
        bones, weights = v['bones'], v['weights']
        if (not 1 <= len(bones) <= 4 or len(bones) != len(weights)
                or len(set(bones)) != len(bones)
                or any(type(b) is not int or not 0 <= b < 15 for b in bones)
                or any(not math.isfinite(w) or w <= 0 for w in weights)
                or not math.isclose(sum(weights), 1, abs_tol=1e-5)):
            errors.append('Invalid body skin binding')
            break
    return errors


def clip_errors(clip, bone_count):
    """Reject malformed tracks even when a manifest hash was refreshed."""
    errors = []
    if not math.isfinite(clip['duration']) or clip['duration'] <= 0 or len(clip['frames']) < 2:
        errors.append('Invalid motion duration/frame count')
    for frame in clip['frames']:
        if len(frame['rotations']) != bone_count or len(frame['feet']) != 2:
            errors.append('Invalid full-body frame binding')
        for q in frame['rotations']:
            values = [q[k] for k in 'xyzw']
            if not all(math.isfinite(v) for v in values) or not math.isclose(sum(v*v for v in values), 1, abs_tol=1e-5):
                errors.append('Invalid motion quaternion')
        if not math.isfinite(frame['pelvisY']) or any(not math.isfinite(f[k]) for f in frame['feet'] for k in 'xyz'):
            errors.append('Nonfinite motion position')
    return errors


def inspect_archives(manifest, directory):
    """Read every member, not just ZIP headers, and inspect retained rig/clip data."""
    packs = []
    for index, pack in enumerate(manifest['packs']):
        archive = directory / pack['archive']
        if digest(archive.read_bytes()) != pack['archiveSha256']:
            raise ValueError('Archive SHA-256 mismatch: ' + pack['archive'])
        with zipfile.ZipFile(archive) as z:
            if len(z.infolist()) != pack['archiveFiles']:
                raise ValueError('Archive member count mismatch')
            members = {n: digest(z.read(n)) for n in z.namelist() if not n.endswith('/')}
            models = []
            for source in manifest['sources']:
                if source['pack'] != index:
                    continue
                data = z.read(source['archiveMember'])
                if digest(data) != source['sha256'] or data != (ROOT / source['path']).read_bytes():
                    raise ValueError('Retained source differs from archive: ' + source['path'])
                suffix = Path(source['path']).suffix
                if suffix not in ('.gltf', '.glb'):
                    continue
                if suffix == '.glb':
                    magic, version, length, size, kind = struct.unpack_from('<5I', data)
                    if (magic, version, length, kind) != (0x46546c67, 2, len(data), 0x4e4f534a):
                        raise ValueError('Invalid source GLB')
                    model = json.loads(data[20:20 + size])
                else:
                    model = json.loads(data)
                nodes = model['nodes']
                models.append({
                    'path': source['path'],
                    'skinJoints': [[nodes[i]['name'] for i in skin['joints']] for skin in model.get('skins', [])],
                    'primitives': sum(len(m['primitives']) for m in model.get('meshes', [])),
                    'animations': [{
                        'name': a['name'], 'channelCount': len(a['channels']),
                        'channels': [{'node': nodes[c['target']['node']]['name'], 'path': c['target']['path'],
                                      'keys': model['accessors'][a['samplers'][c['sampler']]['input']]['count']}
                                     for c in a['channels']] if a['name'] in load('Assets/Resources/Characters/motion-retarget.json')['clips'] else []
                    } for a in model.get('animations', [])]
                })
            packs.append({'archive': pack['archive'], 'sha256': pack['archiveSha256'],
                          'source': pack['source'], 'license': pack['license'],
                          'members': members, 'models': models})
    return {'schema': 1, 'packs': packs}


def verify_current(manifest, runtime, body=None, motion=None, inspection=None):
    body = load(BASE + 'body-bindings.json') if body is None else body
    motion = load(BASE + 'motion-bindings.json') if motion is None else motion
    inspection = load(INSPECTION) if inspection is None else inspection
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    def hashed(entry, path_key='path'):
        path = Path(entry[path_key])
        require(not path.is_absolute() and '..' not in path.parts, 'Unsafe provenance path')
        if path.is_absolute() or '..' in path.parts:
            return
        require((ROOT / path).is_file() and digest((ROOT / path).read_bytes()) == entry['sha256'],
                'Changed/missing provenance file: ' + str(path))

    sources = {s['path']: s for s in manifest['sources']}
    require(len(sources) == len(manifest['sources']), 'Duplicate retained source')
    for source in sources.values():
        hashed(source)
    for pack, inspected in zip(manifest['packs'], inspection['packs']):
        require(pack['license'] == 'CC0-1.0' and pack['edition'] == 'Standard FREE',
                'Expected acquired free CC0 pack')
        require(all(inspected[k] == pack[v] for k, v in [('archive', 'archive'), ('sha256', 'archiveSha256'),
                                                       ('source', 'source'), ('license', 'license')]),
                'Inspection pack identity mismatch')
        require(len(inspected['members']) <= pack['archiveFiles'], 'Invalid inspected member inventory')
    require(len(inspection['packs']) == len(manifest['packs']) == 2, 'Missing inspected archive')
    for source in sources.values():
        require(inspection['packs'][source['pack']]['members'].get(source['archiveMember']) == source['sha256'],
                'Source absent from inspected archive: ' + source['path'])
    for index, filename in enumerate(('0/License_Standard.txt', '1/License.txt')):
        path = BASE + filename
        require(path in sources and sources[path]['pack'] == index,
                'Missing retained license binding')
        require((ROOT / path).is_file() and 'CC0 1.0' in (ROOT / path).read_text(),
                'Missing retained CC0 license bytes')

    styles = {s['id']: s for s in load('Assets/Resources/Characters/appearance.json')['styles']}
    profiles = {p['id']: p for p in load('Assets/Resources/Characters/body-retarget.json')['profiles']}
    expected = {ident for ident, style in styles.items() if style.get('acquiredBody')}
    require({'KOR', 'JPN'} <= expected, 'Missing preserved full-body athletes')
    require(set(profiles) == expected, 'Unused/missing body retarget profile')
    require({o['id'] for o in body['outputs']} == expected and len(body['outputs']) == len(expected),
            'Unused/missing/duplicate target body binding')
    require([r['id'] for r in runtime] == list(styles), 'Missing/duplicate runtime athlete binding')
    consumed = {'Assets/Resources/Characters/Acquired/IdleHead.json',
                'Assets/Resources/Characters/Acquired/FullBodyMotion.json'}
    for entry in runtime:
        hashed(entry, 'mesh')
        style = styles.get(entry['id'])
        if style is None:
            continue
        require(entry['mesh'] == 'Assets/Resources/Athletes/' + entry['id'] + '.asset',
                'Runtime mesh resource mismatch')
        if style.get('acquiredBody'):
            references = {'body': style['acquiredBody']}
        else:
            references = {'head': 'Head' + style['acquiredHead'][4:], 'hair': style['acquiredHair']}
        for key, resource in references.items():
            path = 'Assets/Resources/Characters/Acquired/' + resource + '.json'
            consumed.add(path)
            require(entry.get(key) == path, 'Unused runtime derivative binding: ' + entry['id'])
    adaptations = {a['path'] for a in manifest['adaptations']}
    require(len(adaptations) == len(manifest['adaptations']) and adaptations == consumed,
            'Unused/missing/duplicate derivative inventory')
    for adaptation in manifest['adaptations']:
        hashed(adaptation)
    for binding in (body, motion):
        require(binding['license'] == 'CC0-1.0' and binding['provenance'] == 'provenance.json',
                'Unproven derivative license')
        require((ROOT / binding['method']).is_file(), 'Missing adaptation tool')
    for output in body['outputs']:
        ident = output['id']
        hashed(output, 'output')
        if ident not in profiles or ident not in styles:
            continue
        profile = profiles[ident]
        require(output['output'] == 'Assets/Resources/Characters/Acquired/' + styles[ident]['acquiredBody'] + '.json',
                'Body is not referenced by appearance: ' + ident)
        paths = {s['path'] for s in output['sources']}
        required = {profile['source'], profile['hair'], 'Assets/Resources/Characters/body-retarget.json', BASE + '0/License_Standard.txt'}
        for gltf in [profile['source'], profile['hair']]:
            model = load(gltf)
            required |= {str(Path(gltf).parent / item['uri']) for item in model.get('buffers', [])}
            # The body baker samples base colors; normal/roughness maps and hair textures are unused.
            if gltf == profile['source']:
                for material in model['materials']:
                    texture = material['pbrMetallicRoughness'].get('baseColorTexture')
                    if texture:
                        image = model['images'][model['textures'][texture['index']]['source']]
                        required.add(str(Path(gltf).parent / image['uri']))
        require(required <= paths,
                'Body source/config omitted: ' + ident)
        for entry in output['sources']:
            hashed(entry)
            require(entry['path'] in sources or entry['path'] == 'Assets/Resources/Characters/body-retarget.json',
                    'Body source not from inspected pack')
        require(len(paths) == len(output['sources']), 'Duplicate body source')
        if (ROOT / output['output']).is_file():
            geometry = load(output['output'])
            errors.extend(geometry_errors(geometry))
            require(len(geometry['vertices']) == output['vertices'] and len(geometry['triangles']) == output['triangles'] * 3,
                    'Body geometry inventory mismatch')
        mesh = next((r for r in runtime if r['id'] == ident), None)
        require(mesh is not None, 'Missing runtime athlete: ' + ident)
        if mesh is None:
            continue
        hashed(mesh, 'mesh')
        require(mesh.get('body') == output['output'], 'Missing runtime full-body provenance binding')
        mesh_path = ROOT / mesh['mesh']
        require(mesh_path.is_file() and '-cc0-fullbody-' in mesh_path.read_text(), 'Runtime mesh is not full body')

    config = load('Assets/Resources/Characters/motion-retarget.json')
    files = {f['path'] for f in motion['files']}
    require({config['source'], 'Assets/Resources/Characters/motion-retarget.json',
             'Assets/Resources/Characters/Acquired/FullBodyMotion.json', BASE + '1/License.txt'} == files,
            'Missing motion source/config/output/license')
    for entry in motion['files']:
        hashed(entry)
    require(config['source'] in sources, 'Motion source is not inspected')
    model = next(m for p in inspection['packs'] for m in p['models'] if m['path'] == config['source'])
    animations = {a['name']: a for a in model['animations']}
    clips = load('Assets/Resources/Characters/Acquired/FullBodyMotion.json')['clips']
    require([c['name'] for c in clips] == config['clips'] == [c['name'] for c in motion['clips']],
            'Motion clip identity mismatch')
    require(len(clips) >= 4 and len(config['bones']) >= 10, 'Full-body locomotion missing')
    for clip in clips:
        errors.extend(clip_errors(clip, len(config['bones'])))
        recorded = next((c for c in motion['clips'] if c['name'] == clip['name']), None)
        require(recorded is not None and recorded['frames'] == len(clip['frames']) and recorded['bones'] == len(config['bones']),
                'Motion frame/bone inventory mismatch')
        targets = {c['node'] for c in animations[clip['name']]['channels'] if c['path'] == 'rotation' and c['keys'] > 1}
        require({b['source'] for b in config['bones']} <= targets, 'Missing acquired full-body channels')
    setup = (ROOT / 'Assets/Scripts/Editor/AthleteSkinSetup.cs').read_text()
    player = (ROOT / 'Assets/Scripts/Runtime/AcquiredAthleteMotion.cs').read_text()
    driver = (ROOT / 'Assets/Scripts/Runtime/AthleteMotion.cs').read_text()
    require('Characters/Acquired/"+s.acquiredBody' in setup and 'else AcquiredBody();' in setup,
            'Body not consumed by mesh generator')
    require('Characters/Acquired/FullBodyMotion' in player and 'acquiredMotion.Apply(' in driver,
            'Full-body tracks not consumed by runtime')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archives', type=Path, required=True)
    parser.add_argument('--record-inspection', action='store_true')
    args = parser.parse_args()
    manifest = load(BASE + 'provenance.json')
    inspection = inspect_archives(manifest, args.archives)
    if args.record_inspection:
        (ROOT / INSPECTION).write_text(json.dumps(inspection, indent=2) + '\n')
    if inspection != load(INSPECTION):
        raise SystemExit('Archive inspection differs from retained record')
    errors = verify_current(manifest, load(BASE + 'runtime-bindings.json'))
    if errors:
        raise SystemExit('\n'.join(errors))
    print('Verified two complete CC0 archive inventories, retained sources, configured full bodies and four full-body clips')
