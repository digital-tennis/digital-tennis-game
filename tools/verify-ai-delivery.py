"""Audit documented full-match results against immutable raw evidence and publication."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'docs/evidence/ai-full-match'
AI = ROOT / '.work/ai-full-match'
DOCS = ROOT / '.work/docs-full-match'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(name):
    return json.loads((FOLDER / name).read_text())


def historical(path, digest, commit, archive=None):
    current = ROOT / path
    if current.is_file() and sha(current.read_bytes()) == digest:
        return
    data = (FOLDER / archive).read_bytes() if archive else subprocess.check_output(
        ['git', 'show', commit + ':' + path], cwd=ROOT)
    assert sha(data) == digest, path


def completion(runtime):
    ordinary = bool(runtime['windowInspection']['passed']) and not runtime['nativeBatch']
    complete = bool(runtime['automatedPassed']) and ordinary
    assert runtime['passed'] == complete, 'Inconsistent runtime completion flags'
    return complete


def audit(require_complete=False):
    b = read('baseline.json')
    spec = importlib.util.spec_from_file_location('baseline', ROOT / 'tools/verify-ai-full-match.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.verify(FOLDER / 'baseline.json')
    raw = read('baseline-raw.json')
    cases = module.validate(raw)
    assert b['summary'] == module.summarize(cases)
    assert raw['modelSha256'] == b['provenance']['modelSha256']
    for path, digest in b['evidenceSha256'].items():
        assert sha((FOLDER / path).read_bytes()) == digest, path
    sources = b['provenance']['files']
    assert sha(json.dumps(sources, sort_keys=True).encode()) == b['provenance']['sourceSha256']
    xml = ET.parse(FOLDER / 'baseline-tests.xml').getroot()
    assert xml.get('failed') == '0' and any(
        x.get('fullname') == 'DigitalTennis.Tests.FullMatchBaselineTests.ExpandedPreTrainingBaseline'
        and x.get('result') == 'Passed' for x in xml.iter('test-case'))
    # These audit score/reward/terminal trajectories, not just the report flags.
    for command in [['python3', 'tools/verify-ai-training.py'],
                    ['python3', 'tools/verify-ai-self-play.py'],
                    [module.learning_python(), 'tools/verify-ai-resume.py'],
                    ['python3', 'tools/verify-ai-runtime.py', '--automated-only']]:
        subprocess.run(command, cwd=ROOT, check=True, env=module.verification_environment())
    resume = read('resume.json')
    assert resume['passed'] and resume['additionalSteps'] == 8192
    for path, digest in resume['contract']['hashes'].items():
        if path.startswith(('Training/', 'Runtime/')):
            assert sha((AI / path).read_bytes()) == digest, path
        else:
            historical(path, digest, b['provenance']['gameCommit'],
                       {'tools/unity.sh':'resume-unity.sh','Packages/manifest.json':'runtime-manifest.json'}.get(path))
    runtime = read('runtime.json')
    complete = completion(runtime)
    assert runtime['automatedPassed']
    # Verify .meta pairs and GUID uniqueness for every source asset in the delivery.
    for root in [ROOT / 'Assets', AI / 'Runtime', AI / 'Training', AI / 'tests']:
        for path in root.rglob('*'):
            if any(x in path.parts for x in ['__pycache__', 'bin', 'obj']):
                continue
            if path.suffix in ('.cs', '.asmdef', '.json', '.csv', '.py'):
                assert path.with_name(path.name + '.meta').is_file(), str(path)
    for root in [ROOT / 'Assets', AI]:
        guids = set()
        for path in root.rglob('*.meta'):
            if '.work' in path.relative_to(root).parts or 'bin' in path.parts or 'obj' in path.parts:
                continue
            match = re.search(r'^guid: ([a-f0-9]{32})$', path.read_text(), re.M)
            assert match and match[1] not in guids, str(path)
            guids.add(match[1])
    docs = [ROOT / 'README.md', ROOT / 'docs/README.md', ROOT / 'docs/ai-full-match-results.md',
            AI / 'README.md', AI / 'Training/README.md', AI / 'Training/full-match-delivery.md',
            DOCS / 'README.md', DOCS / 'docs/project/ai-full-match.md',
            DOCS / 'docs/contracts/ai-full-match.md', DOCS / 'docs/project/current-backlog.md']
    for path in docs:
        assert path.is_file(), str(path)
        text = path.read_text()
        assert any(line.startswith('# ') for line in text.splitlines()), str(path)
    print(json.dumps({'evidenceIntegrityPassed': True, 'baselineCases': len(cases),
                      'baselinePoints': b['summary']['overall']['points'],
                      'goalComplete': complete, 'blocker': None if complete else runtime['windowInspection'].get('blocker')}))
    return 3 if require_complete and not complete else 0


def publication():
    delivery = read('delivery.json')
    assert delivery['goalComplete'] == completion(read('runtime.json'))
    for path, digest in delivery['artifactSha256'].items():
        assert sha((FOLDER / path).read_bytes()) == digest, path
    ai_pin=delivery['aiFeatureCommit']
    for repo in delivery['repositories']:
        pr = json.loads(subprocess.check_output(
            ['gh', 'pr', 'view', repo['pr'], '--repo', repo['repository'], '--json',
             'state,mergedAt,mergeCommit,isDraft,headRefOid,url,statusCheckRollup'], text=True))
        merged=pr['state']=='MERGED' and pr['mergedAt'] is not None
        assert merged or (pr['state']=='OPEN' and pr['mergedAt'] is None)
        if merged:
            local=AI if repo['repository'].endswith('-ai') else DOCS if repo['repository'].endswith('-docs') else ROOT
            commit=pr['mergeCommit']['oid']
            # Deleted feature branches are not required: retain only the PR's
            # immutable objects in FETCH_HEAD, without recreating a local branch.
            subprocess.run(['git','fetch','--no-tags','origin','main','refs/pull/'+repo['pr']+'/head'],cwd=local,check=True,stdout=subprocess.DEVNULL)
            subprocess.run(['git','merge-base','--is-ancestor',commit,'origin/main'],cwd=local,check=True)
            trees=[subprocess.check_output(['git','rev-parse',oid+'^{tree}'],cwd=local,text=True).strip() for oid in (pr['headRefOid'],commit)]
            assert trees[0]==trees[1],'Squash tree differs from reviewed PR head'
            subprocess.run(['git','merge-base','--is-ancestor',repo['publishedCommit'],pr['headRefOid']],cwd=local,check=True)
            if repo['repository'].endswith('-ai'):ai_pin=commit
        elif pr['headRefOid'] != repo['publishedCommit']:
            # A receipt cannot name its own enclosing Git commit. Only this
            # self-referential JSON may follow the verified publication commit.
            assert repo['repository'].endswith('-game')
            subprocess.run(['git','merge-base','--is-ancestor',repo['publishedCommit'],pr['headRefOid']],cwd=ROOT,check=True)
            delta=subprocess.check_output(['git','diff','--name-only',repo['publishedCommit'],pr['headRefOid']],cwd=ROOT,text=True).splitlines()
            assert delta==['docs/evidence/ai-full-match/delivery.json'],delta
        if repo['repository'].endswith('-ai') and not merged:
            assert pr['isDraft']
        print(json.dumps({'repository': repo['repository'], 'pr': pr['url'],
                          'head': pr['headRefOid'], 'checks': pr['statusCheckRollup']}))
    pin = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']['com.digital-tennis.ai']
    assert pin.endswith('#' + ai_pin)
    lock = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']['com.digital-tennis.ai']
    assert lock['hash'] == ai_pin and lock['version'] == pin


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--publication', action='store_true')
    parser.add_argument('--require-complete', action='store_true', help='Exit 3 while required ordinary-window inspection is pending')
    args = parser.parse_args()
    code = audit(require_complete=args.require_complete)
    if args.publication:
        publication()
    sys.exit(code)
