#!/usr/bin/env python3
"""Verify retained CC0 archive members, derived geometry, and shipped runtime references."""
import argparse, copy, hashlib, json, re, sys, unittest, zipfile
from athlete_provenance import verify_current
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
PROVENANCE=ROOT/'Assets/ThirdParty/Quaternius/provenance.json'
BINDINGS=ROOT/'Assets/ThirdParty/Quaternius/runtime-bindings.json'
IDS='KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
def sha(b):return hashlib.sha256(b).hexdigest()
def safe(path):
    p=ROOT/path
    if Path(path).is_absolute() or '..' in Path(path).parts:raise ValueError('Non-workspace source')
    return p

def verify(manifest,bindings,archives=None):
    errors=[]
    for p in manifest['packs']:
        if p['license']!='CC0-1.0' or p['edition']!='Standard FREE':errors.append('Expected verified free CC0 Standard')
        if archives:
            a=archives/p['archive']
            if sha(a.read_bytes())!=p['archiveSha256']:errors.append('Archive hash mismatch: '+p['archive'])
    for entry in manifest['sources']+manifest['adaptations']:
        path=safe(entry['path'])
        if not path.is_file() or sha(path.read_bytes())!=entry['sha256']:errors.append('Hash mismatch/missing: '+entry['path'])
        if archives and 'archiveMember' in entry:
            with zipfile.ZipFile(archives/manifest['packs'][entry['pack']]['archive']) as z:
                if sha(z.read(entry['archiveMember']))!=entry['sha256']:errors.append('Not the actual free archive member: '+entry['path'])
    for pack,filename in [(0,'License_Standard.txt'),(1,'License.txt')]:
        entries=[s for s in manifest['sources'] if s['pack']==pack and s['path'].endswith(filename)]
        if not entries or 'CC0' not in safe(entries[0]['path']).read_text():errors.append('Missing source CC0 license')
    if [b['id'] for b in bindings]!=IDS:errors.append('Missing runtime athlete binding')
    for binding in bindings:
        p=safe(binding['mesh'])
        if not p.exists() or sha(p.read_bytes())!=binding['sha256']:errors.append('Runtime mesh changed/missing: '+binding['id'])
        elif '-cc0-' not in p.read_text():errors.append('Runtime mesh lacks acquired generation')
        for key in (['body'] if 'body' in binding else ['head','hair']):
            if binding[key] not in [x['path'] for x in manifest['adaptations']]:errors.append('Unproven runtime '+key)
    runtime=(ROOT/'Assets/Scripts/Runtime/AthleteSkin.cs').read_text()
    if 'Resources.Load<Mesh>("Athletes/"+style.id)' not in runtime:errors.append('Missing runtime resource load')
    idle=json.loads((ROOT/'Assets/Resources/Characters/Acquired/IdleHead.json').read_text())
    if idle['clip']!='Idle_Loop' or idle['joint'].lower()!='head' or len(idle['times'])<10:errors.append('Missing acquired animation')
    if 'AcquiredAthleteIdle.Sample(time+profile)' not in (ROOT/'Assets/Scripts/Runtime/AthleteMotion.cs').read_text():errors.append('Unused acquired animation')
    errors.extend(verify_current(manifest, bindings))
    return errors

def record():
    styles=json.loads((ROOT/'Assets/Resources/Characters/appearance.json').read_text())['styles']
    rows=[]
    for i,ident in enumerate(IDS):
        mesh=f'Assets/Resources/Athletes/{ident}.asset'
        row={'id':ident,'mesh':mesh,'sha256':sha(safe(mesh).read_bytes())}
        if styles[i].get('acquiredBody'):
            row['body']='Assets/Resources/Characters/Acquired/'+styles[i]['acquiredBody']+'.json'
        else:
            row.update(head='Assets/Resources/Characters/Acquired/Head'+styles[i]['acquiredHead'][4:]+'.json',hair='Assets/Resources/Characters/Acquired/'+styles[i]['acquiredHair']+'.json')
        rows.append(row)
    BINDINGS.write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')

class ProvenanceTests(unittest.TestCase):
    def setUp(self):self.manifest=json.loads(PROVENANCE.read_text());self.bindings=json.loads(BINDINGS.read_text())
    def test_actual_shipped_assets(self):self.assertEqual(verify(self.manifest,self.bindings),[])
    def test_corruption_is_rejected(self):
        for key in ['sources','adaptations']:
            m=copy.deepcopy(self.manifest);m[key][0]['sha256']='0'*64;self.assertTrue(verify(m,self.bindings))
    def test_unused_unproven_and_missing_runtime_assets_are_rejected(self):
        b=copy.deepcopy(self.bindings);b[0]['body']='unused.json';self.assertTrue(verify(self.manifest,b))
        b=copy.deepcopy(self.bindings);b[0]['mesh']='Assets/missing.asset';self.assertTrue(verify(self.manifest,b))
        self.assertTrue(verify(self.manifest,self.bindings[:-1]))
    def test_paid_or_non_cc0_provenance_is_rejected(self):
        m=copy.deepcopy(self.manifest);m['packs'][0]['edition']='Source PAID';self.assertTrue(verify(m,self.bindings))
        m=copy.deepcopy(self.manifest);m['packs'][0]['license']='restricted';self.assertTrue(verify(m,self.bindings))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--archives',type=Path);p.add_argument('--record-runtime',action='store_true');args=p.parse_args()
    if args.record_runtime:record()
    errors=verify(json.loads(PROVENANCE.read_text()),json.loads(BINDINGS.read_text()),args.archives)
    for error in errors:print(error,file=sys.stderr)
    if errors:sys.exit(1)
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ProvenanceTests));sys.exit(0 if result.wasSuccessful() else 1)
