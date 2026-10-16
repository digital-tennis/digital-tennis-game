#!/usr/bin/env python3
"""Run the real athlete acceptance checks through a supported unittest entry point.

Requires the retained local .work/ac4 captures and .work/athlete-assets archives.
No game is launched and no report is rewritten by these acceptance checks.
"""
import hashlib
import os
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
REPORTS = [
    ROOT / '.work/ac4/audio/waveform-audit.json',
    ROOT / '.work/ac4/verified.json',
    ROOT / '.work/ac4/review.html',
]


class AthleteAcceptanceTests(unittest.TestCase):
    def run_check(self, script, *args):
        env = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
        run = subprocess.run([sys.executable, '-B', str(ROOT / 'tools' / script), *args],
                             cwd=ROOT, env=env, capture_output=True, text=True, timeout=240)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        print(run.stdout + run.stderr, end='', flush=True)

    def preserved_reports(self):
        return [(p.stat().st_mtime_ns, hashlib.sha256(p.read_bytes()).hexdigest()) for p in REPORTS]

    def test_01_all_fifteen_narratives(self):
        self.run_check('verify-athlete-upgrade.py', 'narrative')

    def test_02_cc0_archives_runtime_geometry_and_animation(self):
        self.run_check('verify-athlete-upgrade.py', 'assets')

    def test_03_actual_audio_without_writes(self):
        before = self.preserved_reports()
        self.run_check('verify-athlete-upgrade.py', 'audio', '--read-only')
        self.assertEqual(before, self.preserved_reports())

    def test_04_native_build_xml_and_447_artifacts_without_writes(self):
        before = self.preserved_reports()
        self.run_check('verify-athlete-upgrade.py', 'native', '--read-only')
        self.assertEqual(before, self.preserved_reports())

    def test_05_missing_evidence_and_invalid_arguments_fail(self):
        self.run_check('test-upgrade-verifier.py')


if __name__ == '__main__':
    unittest.main(verbosity=2)
