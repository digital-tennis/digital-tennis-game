"""Audit documented full-match results against immutable raw evidence and publication."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
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


def audit():
    b = read('baseline.json')
    spec = importlib.util.spec_from_file_location('baseline', ROOT / 'tools/verify-ai-full-match.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    raw = read('baseline-raw.json')
    cases = module.validate(raw)
    assert b['summary'] == module.summarize(cases)
    assert raw['modelSha256'] == b['provenance']['modelSha256']
    for path, digest in b['evidenceSha256'].items():
        assert sha((FOLDER / path).read_bytes()) == digest, path
    sources = b['provenance']['files']
    assert sha(json.dumps(sources, sort_keys=True).encode()) == b['provenance']['sourceSha256']
    for path, digest in sources.items():
        historical(path, digest, b['provenance']['gameCommit'],
                   'baseline-unity.sh' if path == 'tools/unity.sh' else None)
    xml = ET.parse(FOLDER / 'baseline-tests.xml').getroot()
    assert xml.get('failed') == '0' and any(
        x.get('fullname') == 'DigitalTennis.Tests.FullMatchBaselineTests.ExpandedPreTrainingBaseline'
        and x.get('result') == 'Passed' for x in xml.iter('test-case'))
    # These audit score/reward/terminal trajectories, not just the report flags.
    for command in [['python3', 'tools/verify-ai-training.py'],
                    ['python3', 'tools/verify-ai-self-play.py'],
                    ['python3', 'tools/verify-ai-runtime.py', '--automated-only']]:
        subprocess.run(command, cwd=ROOT, check=True)
    resume = read('resume.json')
    assert resume['passed'] and resume['additionalSteps'] == 8192
    for path, digest in resume['contract']['hashes'].items():
        if path.startswith(('Training/', 'Runtime/')):
            assert sha((AI / path).read_bytes()) == digest, path
        else:
            historical(path, digest, b['provenance']['gameCommit'],
                       {'tools/unity.sh':'resume-unity.sh','Packages/manifest.json':'runtime-manifest.json'}.get(path))
    runtime = read('runtime.json')
    assert not runtime['passed'] and not runtime['windowInspection']['passed']
    assert runtime['nativeBatch'] and runtime['automatedPassed']
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
        assert '미완료' in text, str(path)
    print(json.dumps({'auditPassed': True, 'baselineCases': len(cases),
                      'baselinePoints': b['summary']['overall']['points'],
                      'goalComplete': False, 'blocker': runtime['windowInspection']['blocker']}))


def publication():
    delivery = read('delivery.json')
    assert delivery['goalComplete'] is False
    for path, digest in delivery['artifactSha256'].items():
        assert sha((FOLDER / path).read_bytes()) == digest, path
    for repo in delivery['repositories']:
        pr = json.loads(subprocess.check_output(
            ['gh', 'pr', 'view', repo['pr'], '--repo', repo['repository'], '--json',
             'state,mergedAt,isDraft,headRefOid,url,statusCheckRollup'], text=True))
        assert pr['state'] == 'OPEN' and pr['mergedAt'] is None
        if pr['headRefOid'] != repo['publishedCommit']:
            # A receipt cannot name its own enclosing Git commit. Only this
            # self-referential JSON may follow the verified publication commit.
            assert repo['repository'].endswith('-game')
            subprocess.run(['git','merge-base','--is-ancestor',repo['publishedCommit'],pr['headRefOid']],cwd=ROOT,check=True)
            delta=subprocess.check_output(['git','diff','--name-only',repo['publishedCommit'],pr['headRefOid']],cwd=ROOT,text=True).splitlines()
            assert delta==['docs/evidence/ai-full-match/delivery.json'],delta
        if repo['repository'].endswith('-ai'):
            assert pr['isDraft']
        print(json.dumps({'repository': repo['repository'], 'pr': pr['url'],
                          'head': pr['headRefOid'], 'checks': pr['statusCheckRollup']}))
    pin = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']['com.digital-tennis.ai']
    assert pin.endswith('#' + delivery['aiFeatureCommit'])
    lock = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']['com.digital-tennis.ai']
    assert lock['hash'] == delivery['aiFeatureCommit'] and lock['version'] == pin


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--publication', action='store_true')
    args = parser.parse_args()
    audit()
    if args.publication:
        publication()
