"""AC4 evidence integrity: original baseline, unchanged pixels, runtime and recovery."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unittest

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / '.work/proportion-review'
MOTION = ROOT / '.work/ac2-motion'
REVIEW = ROOT / '.work/athlete-comparison'
BASE = '69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0'


class AthleteComparisonTests(unittest.TestCase):
    def assert_pixels(self, a, b):
        self.assertEqual(a.size, b.size)
        self.assertIsNone(ImageChops.difference(a.convert('RGB'), b.convert('RGB')).getbbox())

    def test_baseline_meshes_and_matched_camera_receipts(self):
        for athlete in ('KOR', 'JPN'):
            source = subprocess.check_output(['git', 'show', f'{BASE}:Assets/Resources/Athletes/{athlete}.asset'], cwd=ROOT)
            self.assertEqual((ART / 'originals' / f'{athlete}.asset').read_bytes(), source)
            self.assertIn(hashlib.sha256(source).hexdigest(), (ART / 'before-assets.txt').read_text())
            current = ROOT / f'Assets/Resources/Athletes/{athlete}.asset'
            self.assertIn(hashlib.sha256(current.read_bytes()).hexdigest(), (ART / 'after-assets.txt').read_text())
        before = json.loads((ART / 'before.json').read_text())
        after = json.loads((ART / 'after.json').read_text())
        self.assertEqual(before, after)
        self.assertEqual(before['baselineRevision'], BASE)
        self.assertNotIn('Null', before['graphics'])
        self.assertTrue(before['stagedArtPreview'])

    def test_comparison_sheets_preserve_every_source_pixel(self):
        for athlete in ('KOR', 'JPN'):
            for view in ('front', 'side', 'face'):
                with Image.open(ART / f'comparison-{athlete}-{view}.png') as sheet:
                    self.assertEqual(sheet.size, (1800, 1160))
                    for phase, x in [('before', 0), ('after', 900)]:
                        with Image.open(ART / f'{phase}-{athlete}-{view}.png') as source:
                            self.assert_pixels(source, sheet.crop((x, 60, x + 900, 1160)))

    def test_motion_sheets_original_frames_and_actual_recovery(self):
        receipt = json.loads((MOTION / 'build-receipt.json').read_text())
        dll = ROOT / 'Builds/verification/mac/DigitalTennis.app/Contents/Resources/Data/Managed/DigitalTennis.Runtime.dll'
        self.assertEqual(hashlib.sha256(dll.read_bytes()).hexdigest(), receipt['runtimeSha256'])
        for entry in receipt['sources']:
            self.assertEqual(hashlib.sha256((ROOT / entry['path']).read_bytes()).hexdigest(), entry['sha256'])
        for athlete in ('KOR', 'JPN'):
            for action in ('Running', 'Serve', 'Forehand', 'Backhand'):
                name = f'{athlete}-{action}'
                timeline = json.loads((MOTION / 'motion' / f'{name}-timeline.json').read_text())['samples']
                self.assertEqual([s['frame'] for s in timeline], list(range(16)))
                self.assertTrue(all(a['clock'] < b['clock'] for a, b in zip(timeline, timeline[1:])))
                if action != 'Running':
                    self.assertTrue(timeline[0]['swinging'])
                    self.assertLess(timeline[0]['swingAge'], .06)
                    self.assertTrue(any(not s['swinging'] and s['swingAge'] >= .349 for s in timeline))
                else:
                    self.assertGreater(max(s['speed'] for s in timeline), 1)
                with Image.open(MOTION / f'{name}-sequence.png') as sheet:
                    self.assertEqual(sheet.size, (2400, 1880))
                    for k, frame in enumerate((0, 4, 8, 15)):
                        path = MOTION / 'motion' / f'{name}-{frame:02}.png'
                        self.assertGreater(path.stat().st_mtime, receipt['buildTime'])
                        with Image.open(path) as source:
                            self.assertEqual(source.size, (1200, 900))
                            x, y = k % 2 * 1200, k // 2 * 940 + 40
                            self.assert_pixels(source, sheet.crop((x, y, x + 1200, y + 900)))

    def test_review_links_hashes_and_honest_labels(self):
        page = (REVIEW / 'index.html').read_text()
        manifest = json.loads((REVIEW / 'manifest.json').read_text())
        self.assertEqual(manifest['baselineRevision'], BASE)
        self.assertIn('after only', manifest['motionEvidence'])
        for entry in manifest['files']:
            path = ROOT / entry['path']
            self.assertTrue(path.resolve().is_relative_to(ROOT))
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), entry['sha256'])
        for url in re.findall(r'(?:href|src)="([^"]+)"', page):
            self.assertTrue((REVIEW / url).is_file(), url)
        self.assertGreaterEqual(page.count('RECOVERY / 회복'), 6)
        self.assertNotIn('autoplay', page)
        self.assertIn('staged art preview', page)


if __name__ == '__main__':
    unittest.main()
