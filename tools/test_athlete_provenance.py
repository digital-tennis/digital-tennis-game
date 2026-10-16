"""AC3: archive identity, redistribution rights, derivative and runtime traceability."""
import copy
import importlib.util
from pathlib import Path
import shutil
import tempfile
import unittest

import athlete_provenance as provenance

ROOT = provenance.ROOT
BASE = provenance.BASE


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'tools' / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


class AthleteProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.manifest = provenance.load(BASE + 'provenance.json')
        self.runtime = provenance.load(BASE + 'runtime-bindings.json')
        self.body = provenance.load(BASE + 'body-bindings.json')
        self.motion = provenance.load(BASE + 'motion-bindings.json')
        self.inspection = provenance.load(provenance.INSPECTION)

    def verify(self):
        return provenance.verify_current(self.manifest, self.runtime, self.body, self.motion, self.inspection)

    def test_real_archives_and_all_retained_sources_match_inspection(self):
        archives = ROOT / '.work/athlete-assets'
        self.assertTrue(archives.is_dir(), 'Restore original Standard FREE archives to .work/athlete-assets')
        self.assertEqual(provenance.inspect_archives(self.manifest, archives), self.inspection)
        legacy = module('legacy_provenance', 'verify-athlete-assets.py')
        self.assertEqual(legacy.verify(self.manifest, self.runtime, archives), [])
        self.assertEqual(self.verify(), [])

    def test_archive_corruption_is_rejected(self):
        self.manifest['packs'][0]['archiveSha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'Archive SHA-256 mismatch'):
            provenance.inspect_archives(self.manifest, ROOT / '.work/athlete-assets')

    def test_uninspected_source_and_derivative_tampering_are_rejected(self):
        self.inspection['packs'][0]['members'].clear()
        self.assertTrue(self.verify())
        self.inspection = provenance.load(provenance.INSPECTION)
        for athlete in range(2):
            original = copy.deepcopy(self.body)
            self.body['outputs'][athlete]['sha256'] = '0' * 64
            self.assertTrue(self.verify())
            self.body = original
        self.motion['files'][-1]['sha256'] = '0' * 64
        self.assertTrue(self.verify())

    def test_missing_config_source_license_and_runtime_binding_are_rejected(self):
        original = copy.deepcopy(self.body)
        for path in ['Assets/Resources/Characters/body-retarget.json',
                     self.body['outputs'][0]['sources'][0]['path'],
                     self.body['outputs'][0]['sources'][1]['path'], BASE + '0/License_Standard.txt']:
            self.body['outputs'][0]['sources'] = [s for s in original['outputs'][0]['sources'] if s['path'] != path]
            self.assertTrue(self.verify())
        self.body = original
        original_motion = copy.deepcopy(self.motion)
        self.motion['files'] = [f for f in self.motion['files'] if not f['path'].endswith('License.txt')]
        self.assertTrue(self.verify())
        self.motion = original_motion
        original_runtime = copy.deepcopy(self.runtime)
        self.runtime[0].pop('body')
        self.assertTrue(self.verify())
        self.runtime = original_runtime
        self.runtime[0]['sha256'] = '0' * 64
        self.assertTrue(self.verify())

    def test_restricted_derivative_license_and_head_only_tracks_are_rejected(self):
        self.body['license'] = 'restricted'
        self.assertTrue(self.verify())
        self.body['license'] = 'CC0-1.0'
        animation = next(m for p in self.inspection['packs'] for m in p['models'] if m['animations'])
        for clip in animation['animations']:
            clip['channels'] = [c for c in clip['channels'] if c['node'] == 'Head']
        self.assertTrue(self.verify())

    def test_body_and_full_body_tracks_reproduce_from_inspected_sources(self):
        # Rebuild in isolation; never rewrite shipped assets or retained evidence during tests.
        with tempfile.TemporaryDirectory(prefix='athlete-provenance-') as directory:
            target = Path(directory)
            paths = {s['path'] for s in self.manifest['sources']}
            paths |= {'Assets/Resources/Characters/body-retarget.json', 'Assets/Resources/Characters/motion-retarget.json'}
            for path in paths:
                dest = target / path
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / path, dest)
            (target / 'Assets/Resources/Characters/Acquired').mkdir()
            bodies = module('reproduce_bodies', 'adapt-athlete-bodies.py')
            bodies.ROOT = target
            bodies.CFG = target / 'Assets/Resources/Characters/body-retarget.json'
            for profile in provenance.load('Assets/Resources/Characters/body-retarget.json')['profiles']:
                result = bodies.bake(profile)
                expected = next(o for o in self.body['outputs'] if o['id'] == profile['id'])
                self.assertEqual(result, expected)
            motion = module('reproduce_motion', 'adapt-athlete-motion.py')
            motion.ROOT = target
            motion.main()
            for path in ['Assets/Resources/Characters/Acquired/FullBodyMotion.json', BASE + 'motion-bindings.json']:
                self.assertEqual((target / path).read_bytes(), (ROOT / path).read_bytes())


if __name__ == '__main__':
    unittest.main()
