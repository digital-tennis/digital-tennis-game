"""Historical provenance and completion-gate regressions; not gameplay evidence."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


baseline = load('verify-ai-full-match')
runtime = load('verify-ai-runtime')
delivery = load('verify-ai-delivery')


class HistoricalVerificationTests(unittest.TestCase):
    def test_changed_current_verifier_accepts_only_measured_archival_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'tools').mkdir()
            (root / 'tools/verify-ai-full-match.py').write_bytes(b'new legitimate implementation')
            archive = root / 'evidence'
            archive.mkdir()
            (archive / 'baseline-verifier.py').write_bytes(b'measured original')
            expected = hashlib.sha256(b'measured original').hexdigest()
            with patch.object(baseline, 'ROOT', root):
                baseline.historical_source('tools/verify-ai-full-match.py', expected, 'a' * 40, archive)
                (archive / 'baseline-verifier.py').write_bytes(b'corrupted archival source')
                with self.assertRaisesRegex(ValueError, 'Historical source mismatch'):
                    baseline.historical_source('tools/verify-ai-full-match.py', expected, 'a' * 40, archive)

    def test_current_file_is_not_accepted_after_its_digest_changes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'source.cs').write_bytes(b'changed')
            with patch.object(baseline, 'ROOT', root), patch.object(baseline.subprocess, 'check_output', return_value=b'also wrong'):
                with self.assertRaisesRegex(ValueError, 'Historical source mismatch'):
                    baseline.historical_source('source.cs', 'a' * 64, 'b' * 40, root)

    def test_unsafe_provenance_paths_and_malformed_digests_rejected(self):
        for name, digest in [('../outside', 'a' * 64), ('/outside', 'a' * 64), ('source.cs', 'not-a-digest')]:
            with self.subTest(name=name), self.assertRaises(ValueError):
                baseline.historical_source(name, digest, 'b' * 40, Path('.'))

    def test_existing_baseline_is_not_overwritten_by_rerun(self):
        with tempfile.TemporaryDirectory() as folder:
            out = Path(folder)
            (out / 'baseline.json').write_text('original')
            with patch.object(baseline, 'OUT', out), self.assertRaisesRegex(ValueError, 'already exists'):
                baseline.run()
            self.assertEqual((out / 'baseline.json').read_text(), 'original')


class CompletionTests(unittest.TestCase):
    def test_pending_native_window_is_incomplete_for_both_gates(self):
        report = dict(automatedPassed=True, passed=False, nativeBatch=True, windowInspection={'passed': False})
        for module in (runtime, delivery):
            self.assertFalse(module.completion(report))

    def test_completed_native_window_can_be_accepted(self):
        report = dict(automatedPassed=True, passed=True, nativeBatch=False, windowInspection={'passed': True})
        for module in (runtime, delivery):
            self.assertTrue(module.completion(report))

    def test_falsely_claimed_completion_of_batch_run_is_rejected(self):
        report = dict(automatedPassed=True, passed=True, nativeBatch=True, windowInspection={'passed': True})
        for module in (runtime, delivery):
            with self.assertRaises(AssertionError):
                module.completion(report)

    def test_stale_incomplete_flag_after_native_success_is_rejected(self):
        report = dict(automatedPassed=True, passed=False, nativeBatch=False, windowInspection={'passed': True})
        for module in (runtime, delivery):
            with self.assertRaises(AssertionError):
                module.completion(report)


if __name__ == '__main__':
    unittest.main()
