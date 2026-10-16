#!/usr/bin/env python3
"""Route each athlete upgrade check to its actual narrative, asset, audio or native verifier."""
import argparse
import copy
import json
from pathlib import Path
import sys
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
ROSTER = ROOT / 'Assets/Resources/Characters/roster.json'
STORIES = ROOT / 'docs/athlete-stories.md'
IDS = 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
NAMES = ['한서윤', '아오야마 렌', '린웨', '마야 브룩스', '니콜라이 소콜로프',
         '아라브 라오', '레나 베버', '테오 베넷', '루시아 세라노', '카미유 로랑',
         '카이 모건', '노르 더프리스', '하파엘 코스타', '살마 암라니', '타보 은들로부']
LABELS = {
    'persona': '페르소나', 'formativeIncident': '전환 사건',
    'motivationStakes': '목표와 걸린 것', 'flaw': '약점', 'growth': '성장',
    'relationship': '관계와 라이벌', 'voiceIdentity': '목소리',
    'outfitIdentity': '의상', 'accessoryIdentity': '소품',
    'skillIdentity': '스킬', 'courtIdentity': '코트', 'musicIdentity': '음악',
}


def verify(profiles, documentation):
    """Cross-check published content and references, with no arbitrary length quota."""
    errors = []
    if [p.get('id') for p in profiles] != IDS:
        errors.append('Expected all 15 established IDs in KOR/JPN-first order')
    if [p.get('localName') for p in profiles] != NAMES:
        errors.append('Established names changed or missing')
    peers = {p.get('id'): p for p in profiles}
    seen = {field: set() for field in LABELS}
    for p in profiles:
        ident = p.get('id', '?')
        if not isinstance(p.get('age'), int) or p['age'] < 18:
            errors.append(f'{ident}: expected adult athlete')
        n = p.get('narrative')
        if not isinstance(n, dict):
            errors.append(f'{ident}: missing narrative')
            continue
        heading = f"## {ident} · {p.get('localName')} ("
        section = documentation.split(heading, 1)
        section = section[1].split('\n## ', 1)[0] if len(section) == 2 else ''
        for field, label in LABELS.items():
            value = n.get(field)
            if not isinstance(value, str) or not value.strip():
                errors.append(f'{ident}: missing {field}')
                continue
            if value in seen[field]:
                errors.append(f'{ident}: repeated {field}')
            seen[field].add(value)
            if f'- **{label}**: {value}' not in section:
                errors.append(f'{ident}: documentation differs for {field}')
        peer = peers.get(n.get('relationshipId'))
        if peer is None or peer is p or peer.get('localName', '') not in n.get('relationship', ''):
            errors.append(f'{ident}: relationship must name another existing athlete')
        for field, target in [('skillIdentity', 'skillName'), ('courtIdentity', 'courtName')]:
            if not p.get(target) or p[target] not in n.get(field, ''):
                errors.append(f'{ident}: {field} must connect to established {target}')
        if '메뉴·소개·결과' not in n.get('musicIdentity', ''):
            errors.append(f'{ident}: music identity must retain presentation-only scope')
        if '대표 대사:' not in n.get('voiceIdentity', ''):
            errors.append(f'{ident}: missing persona-specific representative line')
    for ident, peer_id, incident in [('KOR', 'JPN', '휠체어'), ('JPN', 'KOR', '습기')]:
        n = peers.get(ident, {}).get('narrative') or {}
        if n.get('relationshipId') != peer_id or incident not in n.get('formativeIncident', ''):
            errors.append(f'{ident}: central workshop arc lost')
    return errors


class NarrativeVerifierTests(unittest.TestCase):
    def setUp(self):
        self.profiles = json.loads(ROSTER.read_text(encoding='utf-8'))['characters']
        self.docs = STORIES.read_text(encoding='utf-8')

    def test_published_catalog_and_korean_documentation(self):
        self.assertEqual(verify(self.profiles, self.docs), [])

    def test_rejects_missing_athlete_and_changed_identity(self):
        for change in ('missing', 'renamed', 'minor'):
            with self.subTest(change=change):
                profiles = copy.deepcopy(self.profiles)
                if change == 'missing':
                    profiles.pop()
                elif change == 'renamed':
                    profiles[0]['localName'] = '다른 선수'
                else:
                    profiles[0]['age'] = 17
                self.assertTrue(verify(profiles, self.docs))

    def test_rejects_missing_fields_for_every_athlete(self):
        for index in range(15):
            for field in LABELS:
                with self.subTest(athlete=IDS[index], field=field):
                    profiles = copy.deepcopy(self.profiles)
                    profiles[index]['narrative'][field] = ''
                    self.assertTrue(verify(profiles, self.docs))

    def test_rejects_broken_relationship_and_presentation_links(self):
        for field, value in [('relationshipId', 'UNKNOWN'), ('relationshipId', 'KOR'),
                             ('relationship', '이름 없는 라이벌'), ('skillIdentity', '다른 기술'),
                             ('courtIdentity', '다른 코트'), ('musicIdentity', '경기 중 음악')]:
            with self.subTest(field=field, value=value):
                profiles = copy.deepcopy(self.profiles)
                profiles[0]['narrative'][field] = value
                self.assertTrue(verify(profiles, self.docs))

    def test_rejects_repeated_origin_and_stale_documentation(self):
        profiles = copy.deepcopy(self.profiles)
        profiles[2]['narrative']['formativeIncident'] = profiles[3]['narrative']['formativeIncident']
        self.assertTrue(verify(profiles, self.docs))
        self.assertTrue(verify(self.profiles, self.docs.replace('빈자리도 한 획이야.', '오래된 대사.')))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.set_defaults(stage='narrative')
    stages = parser.add_subparsers(dest='stage')
    stages.add_parser('narrative', help='Validate all 15 stories, identities and documentation')
    assets = stages.add_parser('assets', help='Validate actual CC0 archives, adapted geometry and runtime bindings')
    assets.add_argument('--archives', type=Path, default=ROOT / '.work/athlete-assets')
    audio = stages.add_parser('audio', help='Validate source WAVs, licenses and actual fresh/existing listener output')
    audio.add_argument('--out', type=Path, default=ROOT / '.work/ac4/audio')
    native = stages.add_parser('native', help='Validate actual build, test XML, camera/audio and online evidence')
    native.add_argument('--out', type=Path, default=ROOT / '.work/ac4')
    args = parser.parse_args()
    if args.stage in (None, 'narrative'):
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(NarrativeVerifierTests)
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        return 0 if result.wasSuccessful() else 1
    if args.stage == 'assets':
        command = ['verify-athlete-assets.py', '--archives', str(args.archives.resolve())]
    elif args.stage == 'audio':
        command = ['verify-court-audio.py', '--native', str(args.out.resolve())]
    else:
        command = ['verify-native-athlete-review.py', '--out', str(args.out.resolve())]
    print('Running ' + args.stage + ' evidence verifier: ' + command[0], flush=True)
    return subprocess.run([sys.executable, str(ROOT / 'tools' / command[0]), *command[1:]], cwd=ROOT).returncode


if __name__ == '__main__':
    sys.exit(main())
