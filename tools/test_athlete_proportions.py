"""AC1: verify full-body runtime binding, retained CC0 sources and real matched Unity renders.
Local comparison captures are required; never substitutes copied historical runtime evidence.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import unittest

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / '.work/proportion-review'
BASE = '69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class AthleteProportionTests(unittest.TestCase):
    def test_cc0_sources_and_runtime_full_body_binding(self):
        binding = json.loads((ROOT / 'Assets/ThirdParty/Quaternius/body-bindings.json').read_text())
        self.assertEqual(binding['license'], 'CC0-1.0')
        license_text = (ROOT / 'Assets/ThirdParty/Quaternius/0/License_Standard.txt').read_text()
        self.assertIn('CC0 1.0 Universal', license_text)
        styles = json.loads((ROOT / 'Assets/Resources/Characters/appearance.json').read_text())['styles']
        for entry, style in zip(binding['outputs'], styles[:2]):
            self.assertEqual(sha(ROOT / entry['output']), entry['sha256'])
            for source in entry['sources']:
                self.assertEqual(sha(ROOT / source['path']), source['sha256'])
            geometry = json.loads((ROOT / entry['output']).read_text())
            self.assertGreater(len(geometry['vertices']), 8000)
            self.assertGreater(len(geometry['triangles']), 30000)
            self.assertEqual(style['acquiredBody'], 'Body' + entry['id'])
            weights = geometry['vertices']
            for vertex in weights:
                self.assertLessEqual(len(vertex['bones']), 4)
                self.assertAlmostEqual(sum(vertex['weights']), 1, places=5)
                self.assertTrue(np.isfinite([vertex['x'], vertex['y'], vertex['z']]).all())
            self.assertEqual({b for v in weights for b in v['bones']}, set(range(15)))
            mesh = ROOT / ('Assets/Resources/Athletes/' + entry['id'] + '.asset')
            self.assertIn('-cc0-fullbody-', mesh.read_text())
            self.assertNotEqual(mesh.read_bytes(), subprocess.check_output(['git', 'show', BASE + ':' + str(mesh.relative_to(ROOT))], cwd=ROOT))

    def test_other_thirteen_and_personas_are_preserved(self):
        old = json.loads(subprocess.check_output(['git', 'show', BASE + ':Assets/Resources/Characters/appearance.json'], cwd=ROOT))['styles']
        now = json.loads((ROOT / 'Assets/Resources/Characters/appearance.json').read_text())['styles']
        self.assertEqual(old[2:], now[2:])
        for profile in old[2:]:
            path = 'Assets/Resources/Athletes/' + profile['id'] + '.asset'
            self.assertEqual((ROOT / path).read_bytes(), subprocess.check_output(['git', 'show', BASE + ':' + path], cwd=ROOT))
        path = 'Assets/Resources/Characters/roster.json'
        self.assertEqual((ROOT / path).read_bytes(), subprocess.check_output(['git', 'show', BASE + ':' + path], cwd=ROOT))
        for a, b in zip(old[:2], now[:2]):
            self.assertEqual(a, {k: b[k] for k in a})

    def test_unretouched_matched_unity_renders(self):
        for phase in ['before', 'after']:
            manifest = json.loads((REVIEW / (phase + '.json')).read_text())
            self.assertTrue(manifest['stagedArtPreview'])
            self.assertEqual(manifest['baselineRevision'],BASE)
            self.assertNotIn('Null', manifest['graphics'])
            for athlete in ['KOR', 'JPN']:
                for view in ['front', 'side', 'face']:
                    image = Image.open(REVIEW / f'{phase}-{athlete}-{view}.png').convert('RGB')
                    self.assertEqual(image.size, (900, 1100))
                    pixels = np.asarray(image).astype(int)
                    visible = np.max(np.abs(pixels - pixels[0, 0]), axis=2) > 25
                    self.assertGreater(visible.sum(), 30000, f'{phase} {athlete} {view} blank render')
        for athlete in ['KOR', 'JPN']:
            pixels=np.asarray(Image.open(REVIEW/f'after-{athlete}-front.png')).astype(float)[200:800]
            r,g,b=pixels[:,:,0],pixels[:,:,1],pixels[:,:,2]
            foreground=np.max(abs(pixels-pixels[0,0]),axis=2)>25
            self.assertGreater(np.count_nonzero(foreground & (r>g*1.6) & (r>b*1.2) & (r>50)),1000,athlete+' coral design missing')
            self.assertGreater(np.count_nonzero(foreground & (b>r*1.3) & (b>g*1.1)),5000,athlete+' navy/indigo design missing')
            receipt=(REVIEW/'after-assets.txt').read_text()
            self.assertIn(athlete+' '+sha(ROOT/f'Assets/Resources/Athletes/{athlete}.asset'),receipt)
            for view in ['front', 'side', 'face']:
                a = np.asarray(Image.open(REVIEW / f'before-{athlete}-{view}.png')).astype(int)
                b = np.asarray(Image.open(REVIEW / f'after-{athlete}-{view}.png')).astype(int)
                self.assertGreater(np.count_nonzero(np.max(abs(a-b), axis=2) > 12), 20000)
        for key in ['width', 'height', 'orthographicBody', 'orthographicFace', 'graphics']:
            self.assertEqual(json.loads((REVIEW/'before.json').read_text())[key], json.loads((REVIEW/'after.json').read_text())[key])


if __name__ == '__main__':
    unittest.main(verbosity=2)
