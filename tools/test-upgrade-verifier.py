#!/usr/bin/env python3
"""Regress the CLI boundary: no evidence stage may silently run narrative-only tests."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class EvidenceRoutingTests(unittest.TestCase):
    def test_missing_assets_audio_and_native_evidence_fail_in_the_selected_verifier(self):
        with tempfile.TemporaryDirectory(prefix='missing-evidence-') as folder:
            for stage, option in [('assets', '--archives'), ('audio', '--out'), ('native', '--out')]:
                with self.subTest(stage=stage):
                    result = subprocess.run([sys.executable, str(ROOT / 'tools/verify-athlete-upgrade.py'), stage, option, folder, *(['--read-only'] if stage in ('audio', 'native') else [])], cwd=ROOT, capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0, result.stdout)
                    self.assertIn('Running ' + stage + ' evidence verifier:', result.stdout)
                    self.assertNotIn('test_published_catalog_and_korean_documentation', result.stderr)

    def test_unknown_stage_or_misspelled_option_is_rejected(self):
        for args in [['unrecognized'], ['audio', '--ouut', '.work/ac4/audio']]:
            result = subprocess.run([sys.executable, str(ROOT / 'tools/verify-athlete-upgrade.py'), *args], cwd=ROOT, capture_output=True, text=True)
            self.assertEqual(result.returncode, 2)


if __name__ == '__main__':
    unittest.main(verbosity=2)
