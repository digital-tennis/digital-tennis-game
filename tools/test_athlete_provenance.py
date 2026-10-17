"""Archive identity, redistribution rights, derivative and runtime traceability."""
import copy
import importlib.util
import subprocess
import sys
import unittest
from unittest.mock import patch

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
        for athlete in range(len(self.body['outputs'])):
            original = copy.deepcopy(self.body)
            self.body['outputs'][athlete]['sha256'] = '0' * 64
            self.assertTrue(self.verify())
            self.body = original
        self.motion['files'][-1]['sha256'] = '0' * 64
        self.assertTrue(self.verify())

    def test_every_configured_body_requires_one_consumed_binding(self):
        # Any newly configured athlete must have its own consumed body binding.
        removed = self.body['outputs'].pop()
        self.assertIn('Unused/missing/duplicate target body binding', self.verify())
        self.body['outputs'].append(removed)
        self.body['outputs'].append(copy.deepcopy(self.body['outputs'][0]))
        self.assertIn('Unused/missing/duplicate target body binding', self.verify())
        self.body['outputs'][-1]['id'] = 'unused'
        self.assertIn('Unused/missing/duplicate target body binding', self.verify())

    def test_all_retained_sources_and_license_bindings_are_checked(self):
        self.manifest['sources'][-1]['sha256'] = '0' * 64
        self.assertTrue(self.verify())
        self.manifest = provenance.load(BASE + 'provenance.json')
        self.manifest['sources'] = [s for s in self.manifest['sources'] if not s['path'].endswith('License.txt')]
        self.assertIn('Missing retained license binding', self.verify())

    def test_unused_derivatives_and_wrong_runtime_resource_are_rejected(self):
        self.manifest['adaptations'].append(copy.deepcopy(self.manifest['adaptations'][0]))
        self.assertIn('Unused/missing/duplicate derivative inventory', self.verify())
        self.manifest = provenance.load(BASE + 'provenance.json')
        legacy = self.runtime[-1]
        legacy['body'] = 'Assets/Resources/Characters/Acquired/BodyKOR.json'
        self.assertIn('Unused runtime derivative binding: ' + legacy['id'], self.verify())
        self.runtime = provenance.load(BASE + 'runtime-bindings.json')
        self.runtime[0]['mesh'] = self.runtime[1]['mesh']
        self.assertIn('Runtime mesh resource mismatch', self.verify())

    def test_invalid_geometry_and_skin_weights_are_rejected(self):
        mesh = provenance.load(self.body['outputs'][0]['output'])
        self.assertEqual(provenance.geometry_errors(mesh), [])
        for change in ('index', 'weight', 'bone', 'finite'):
            broken = copy.deepcopy(mesh)
            if change == 'index': broken['triangles'][0] = len(mesh['vertices'])
            if change == 'weight': broken['vertices'][0]['weights'][0] = .1
            if change == 'bone': broken['vertices'][0]['bones'][0] = 15
            if change == 'finite': broken['vertices'][0]['x'] = float('nan')
            self.assertTrue(provenance.geometry_errors(broken), change)

    def test_invalid_animation_frames_and_rotations_are_rejected(self):
        clip = provenance.load('Assets/Resources/Characters/Acquired/FullBodyMotion.json')['clips'][0]
        bones = len(provenance.load('Assets/Resources/Characters/motion-retarget.json')['bones'])
        self.assertEqual(provenance.clip_errors(clip, bones), [])
        for change in ('duration', 'binding', 'quaternion', 'finite'):
            broken = copy.deepcopy(clip)
            if change == 'duration': broken['duration'] = 0
            if change == 'binding': broken['frames'][0]['rotations'].pop()
            if change == 'quaternion': broken['frames'][0]['rotations'][0]['w'] = 10
            if change == 'finite': broken['frames'][0]['feet'][0]['x'] = float('nan')
            self.assertTrue(provenance.clip_errors(broken, bones), change)

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

    def test_reproduction_without_dependencies_fails_with_setup_command(self):
        result = subprocess.run(
            [sys.executable, '-S', '-B', str(ROOT / 'tools/reproduce-athlete-provenance.py')],
            cwd=ROOT, text=True, capture_output=True, timeout=30)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('python3 -m venv .work/verification-venv', result.stderr)
        self.assertIn('pip install -r tools/requirements-verification.txt', result.stderr)

    def test_body_and_full_body_tracks_reproduce_from_inspected_sources(self):
        # The harness Python need not carry the numerical asset-authoring stack.
        # Run the complete byte-for-byte rebuild under the documented environment.
        python = ROOT / '.work/verification-venv/bin/python3'
        result = subprocess.run(
            [str(python) if python.is_file() else sys.executable, '-B',
             str(ROOT / 'tools/reproduce-athlete-provenance.py')],
            cwd=ROOT, text=True, capture_output=True, timeout=300)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)



if __name__ == '__main__':
    unittest.main()
