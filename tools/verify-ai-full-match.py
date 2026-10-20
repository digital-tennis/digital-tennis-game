#!/usr/bin/env python3
"""Run/verify the pre-training actual Unity cohort. No training or balance writes."""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
IDS = 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
LEVELS = ('Easy', 'Medium', 'Hard')
SEEDS = (17, 4016)
OUT = ROOT / 'docs/evidence/ai-full-match'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON field: ' + key)
            result[key] = value
        return result
    return json.loads(path.read_text(), object_pairs_hook=pairs,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError('Nonfinite: ' + x)))


def provenance():
    paths = sorted(set(ROOT.glob('Assets/**/*.cs')) | set(ROOT.glob('Assets/**/*.unity')) |
                   set(ROOT.glob('Assets/**/*.asmdef')) | set(ROOT.glob('Assets/StreamingAssets/**/*.csv')) |
                   set(ROOT.glob('Packages/*.json')) | set(ROOT.glob('ProjectSettings/*')) |
                   {ROOT / 'tools/unity.sh', Path(__file__).resolve()})
    hashes = {str(p.relative_to(ROOT)): digest(p.read_bytes()) for p in paths if p.is_file()}
    models = list(ROOT.glob('Library/PackageCache/com.digital-tennis.ai*/Runtime/Resources/DigitalTennisAI/placement-ppo.json'))
    require(len(models) == 1, 'Expected one resolved pinned bundled model')
    return {'gameCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
            'aiDependency': read_json(ROOT / 'Packages/manifest.json')['dependencies']['com.digital-tennis.ai'],
            'sourceSha256': digest(json.dumps(hashes, sort_keys=True).encode()), 'files': hashes,
            'modelSha256': digest(models[0].read_bytes()),
            'balanceSha256': {name: hashes['Assets/StreamingAssets/Balance/' + name + '.csv']
                              for name in ('characters', 'gameplay', 'rules')}}


def validate(raw):
    require(raw.get('schema') == 1 and raw.get('passed') is True, 'Incomplete Unity run')
    require(raw.get('unityVersion') == '6000.3.26f1', 'Wrong Unity version')
    require(isinstance(raw.get('modelSha256'), str) and len(raw['modelSha256']) == 64, 'Missing model hash')
    cases = raw.get('cases')
    require(isinstance(cases, list) and len(cases) == 90, 'Expected 90 complete cases')
    expected = {(near, IDS[(i + 1) % 15], level, seed)
                for i, near in enumerate(IDS) for level in LEVELS for seed in SEEDS}
    seen = set()
    for c in cases:
        key = tuple(c[k] for k in ('near', 'far', 'difficulty', 'seed'))
        require(key in expected and key not in seen, 'Unexpected/duplicate cohort cell: ' + str(key))
        seen.add(key)
        fields = ('nearServes', 'farServes', 'nearHits', 'farHits', 'attempts', 'rejected', 'decisions',
                  'endChanges', 'serviceChanges', 'nearGames', 'farGames', 'points', 'physicsTicks')
        for field in fields:
            require(type(c[field]) is int and c[field] >= 0, 'Invalid counter: ' + field)
        for field, size in (('actions', 6), ('swingResults', 10), ('ballEvents', 4)):
            require(isinstance(c[field], list) and len(c[field]) == size and
                    all(type(v) is int and v >= 0 for v in c[field]), 'Invalid histogram: ' + field)
        require(sum(c['actions']) == c['decisions'], 'Action distribution mismatch')
        require(c['attempts'] == c['farHits'] + c['rejected'], 'Rejected AI actions omitted')
        require(c['swingResults'][0] == c['nearHits'] + c['farHits'], 'Hit distribution mismatch')
        require(sum(c['swingResults'][1:]) >= c['rejected'], 'Missing rejected inputs')
        require(c['nearGames'] + c['farGames'] == 2 and c['points'] >= 8, 'Incomplete service games')
        require(c['nearServes'] >= 4 and c['farServes'] >= 4 and c['serviceChanges'] >= 1, 'Missing service coverage')
        require(c['endChanges'] == 1 and c['physicsTicks'] > 0 and sum(c['ballEvents']) > 0, 'Missing authoritative physics/end change')
        require(len(c['trajectory']) == c['points'], 'Missing points')
        previous = (0, 0)
        rallies = set()
        for point in c['trajectory']:
            near, far = point['nearPoints'], point['farPoints']
            require(type(near) is int and type(far) is int and
                    (near - previous[0], far - previous[1]) in ((1, 0), (0, 1)), 'Malformed point progression')
            require(type(point['rally']) is int and point['rally'] > 0 and point['rally'] not in rallies, 'Duplicate rally')
            rallies.add(point['rally'])
            require(point['server'] in ('Near', 'Far') and point['endChanges'] in (0, 1), 'Invalid server/end count')
            require((point['nearEnd'], point['farEnd']) == (('Near', 'Far') if point['endChanges'] == 0 else ('Far', 'Near')), 'Identity/end mismatch')
            require(point['nearSets'] == point['farSets'] == 0 and
                    0 <= point['nearGames'] <= 2 and 0 <= point['farGames'] <= 2, 'Invalid game/set state')
            previous = (near, far)
        last = c['trajectory'][-1]
        require(last['nearGames'] == c['nearGames'] and last['farGames'] == c['farGames'], 'Final score mismatch')
    require(seen == expected, 'Missing coverage')
    return cases


def summarize(cases):
    def group(rows):
        return {'cases': len(rows), **{field: sum(c[field] for c in rows) for field in
                ('points', 'nearHits', 'farHits', 'attempts', 'rejected', 'decisions', 'nearServes', 'farServes', 'endChanges')},
                'actions': [sum(c['actions'][i] for c in rows) for i in range(6)]}
    return {'overall': group(cases),
            'byCharacter': {role: {name: group([c for c in cases if c[role] == name]) for name in IDS} for role in ('near', 'far')},
            'byMatchup': {near + '/' + IDS[(i + 1) % 15]: group([c for c in cases if c['near'] == near]) for i, near in enumerate(IDS)},
            'byDifficulty': {level: group([c for c in cases if c['difficulty'] == level]) for level in LEVELS}}


def run():
    require(not (OUT / 'baseline.json').exists(),
            'Historical baseline already exists; preserve it before starting a separate baseline run')
    before = provenance()
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'baseline-verifier.py').write_bytes(Path(__file__).read_bytes())
    models = list(ROOT.glob('Library/PackageCache/com.digital-tennis.ai*/Runtime/Resources/DigitalTennisAI/placement-ppo.json'))
    model = next(p for p in models if digest(p.read_bytes()) == before['modelSha256'])
    (OUT / 'baseline-model.json').write_bytes(model.read_bytes())
    subprocess.run(['bash', 'tools/unity.sh', 'test-ai-baseline'], cwd=ROOT, check=True)
    require(before == provenance(), 'Source/model/balance changed during baseline')
    raw_path = ROOT / '.work/ai-full-match-baseline-raw.json'
    raw = read_json(raw_path)
    cases = validate(raw)
    require(raw['modelSha256'] == before['modelSha256'], 'Unity loaded another model')
    xml_path = ROOT / '.work/test-ai-baseline.xml'
    test = ET.parse(xml_path).getroot()
    require(test.get('result') == 'Passed' and int(test.get('failed', '-1')) == 0, 'Unity tests failed')
    require(any(x.get('fullname') == 'DigitalTennis.Tests.FullMatchBaselineTests.ExpandedPreTrainingBaseline' and
                x.get('result') == 'Passed' for x in test.iter('test-case')), 'Baseline test did not pass')
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'baseline-raw.json').write_bytes(raw_path.read_bytes())
    (OUT / 'baseline-tests.xml').write_bytes(xml_path.read_bytes())
    log = (ROOT / '.work/logs/test-ai-baseline.log').read_text(errors='replace')
    lines = [line for line in log.splitlines() if 'AI_BASELINE_CASE ' in line or 'Test Run' in line or 'Exiting batchmode' in line]
    (OUT / 'baseline-unity.log').write_text('\n'.join(lines) + '\n')
    report = {'schema': 1, 'passed': True, 'phase': 'pre-training', 'scope': raw['scope'],
              'limitations': ['Two service games per case, not completed default-rules matches.',
                              'Bounded cyclic offset-one cohort, not all 225 ordered pairs.',
                              'Scripted normalized Near inputs; these results are not human win rates.',
                              'No new training, balance tuning, hardware, Windows or WAN verification.'],
              'provenance': before, 'summary': summarize(cases),
              'evidenceSha256': {name: digest((OUT / name).read_bytes()) for name in
                                 ('baseline-raw.json', 'baseline-tests.xml', 'baseline-unity.log')}}
    (OUT / 'baseline.json').write_text(json.dumps(report, indent=2) + '\n')
    verify(OUT / 'baseline.json', strict_current=True)


def historical_source(relative, expected, commit, folder):
    """Resolve measured bytes, without pretending a new HEAD was the old run."""
    name = Path(relative)
    require(not name.is_absolute() and '..' not in name.parts, 'Unsafe source path')
    require(re.fullmatch(r'[a-f0-9]{64}', expected) is not None, 'Invalid source digest')
    current = ROOT / name
    if current.is_file() and digest(current.read_bytes()) == expected:
        return
    archive = {'tools/unity.sh': 'baseline-unity.sh',
               'tools/verify-ai-full-match.py': 'baseline-verifier.py'}.get(relative)
    if archive:
        original = (folder / archive).read_bytes()
    else:
        require(re.fullmatch(r'[a-f0-9]{40}', commit) is not None, 'Invalid source commit')
        original = subprocess.check_output(['git', 'show', commit + ':' + relative], cwd=ROOT)
    require(digest(original) == expected, 'Historical source mismatch: ' + relative)


def verify(path, strict_current=False):
    path = Path(path)
    report = read_json(path)
    require(report['passed'] is True and report['phase'] == 'pre-training', 'Not a passed baseline')
    for name, sha in report['evidenceSha256'].items():
        require(Path(name).name == name and digest((path.parent / name).read_bytes()) == sha, 'Evidence hash mismatch')
    raw = read_json(path.parent / 'baseline-raw.json')
    cases = validate(raw)
    require(report['summary'] == summarize(cases), 'Summary differs from real cases')
    require(raw['modelSha256'] == report['provenance']['modelSha256'], 'Model provenance mismatch')
    recorded = report['provenance']
    require(digest(json.dumps(recorded['files'], sort_keys=True).encode()) == recorded['sourceSha256'],
            'Historical source manifest mismatch')
    for name, sha in recorded['files'].items():
        historical_source(name, sha, recorded['gameCommit'], path.parent)
    for name, sha in recorded['balanceSha256'].items():
        require(sha == recorded['files']['Assets/StreamingAssets/Balance/' + name + '.csv'],
                'Historical balance manifest mismatch')
    model = path.parent / 'baseline-model.json'
    require(digest(model.read_bytes()) == recorded['modelSha256'], 'Historical model mismatch')
    xml = ET.parse(path.parent / 'baseline-tests.xml').getroot()
    require(xml.get('failed') == '0' and any(
        c.get('fullname') == 'DigitalTennis.Tests.FullMatchBaselineTests.ExpandedPreTrainingBaseline'
        and c.get('result') == 'Passed' for c in xml.iter('test-case')), 'Baseline Unity test missing')
    if strict_current:
        require(recorded == provenance(), 'Current source/model/config differs from historical baseline')
    print(json.dumps({'passed': True, 'cases': len(cases), 'points': report['summary']['overall']['points']}))
    return report


def learning_python(explicit=None):
    """Use the installed pinned environment; never install packages during verification."""
    configured = explicit or os.environ.get('DIGITAL_TENNIS_TRAINING_PYTHON')
    if configured:
        require(Path(configured).is_file(), 'Training Python does not exist')
        return str(Path(configured).absolute())
    ai = ROOT / '.work/ai-full-match'
    candidates = [ai / '.work/mlagents-venv/bin/python', ai / '.venv/bin/python']
    if ai.exists():
        common = Path(subprocess.check_output(['git', 'rev-parse', '--git-common-dir'], cwd=ai, text=True).strip())
        if not common.is_absolute():
            common = ai / common
        candidates.append(common.resolve().parent / '.work/mlagents-venv/bin/python')
    for candidate in candidates:
        if candidate.is_file():
            # Keep the venv executable path: resolving its symlink loses the venv prefix.
            return str(candidate.absolute())
    return sys.executable


def verification_environment():
    environment = os.environ.copy()
    if 'MPLCONFIGDIR' not in environment:
        cache = ROOT / '.work/matplotlib'
        cache.mkdir(parents=True, exist_ok=True)
        environment['MPLCONFIGDIR'] = str(cache)
    return environment


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-baseline', action='store_true')
    parser.add_argument('--baseline', type=Path, default=OUT / 'baseline.json')
    parser.add_argument('--strict-current-source', action='store_true')
    parser.add_argument('--phase', choices=('baseline', 'training', 'self-play', 'resume', 'runtime', 'delivery'), default='baseline')
    parser.add_argument('--python', help='Pinned training interpreter for optimizer/checkpoint validation')
    args = parser.parse_args()
    if args.run_baseline:
        require(args.phase == 'baseline', '--run-baseline requires baseline phase')
        run()
    elif args.phase == 'baseline':
        verify(args.baseline, strict_current=args.strict_current_source)
    else:
        name = {'training': 'training', 'self-play': 'self-play', 'resume': 'resume',
                'runtime': 'runtime', 'delivery': 'delivery'}[args.phase]
        command = [learning_python(args.python) if name == 'resume' else sys.executable,
                   str(ROOT / ('tools/verify-ai-' + name + '.py'))]
        if name == 'delivery':
            command.append('--require-complete')
        return subprocess.run(command, cwd=ROOT, env=verification_environment()).returncode
    return 0


if __name__ == '__main__':
    sys.exit(main())
