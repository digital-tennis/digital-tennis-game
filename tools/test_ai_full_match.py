"""Negative tests for the cohort evidence validator; fixtures are not gameplay evidence."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('baseline', Path(__file__).with_name('verify-ai-full-match.py'))
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)


def fixture():
    cases = []
    for level in baseline.LEVELS:
        for seed in baseline.SEEDS:
            for i, near in enumerate(baseline.IDS):
                trajectory = [dict(rally=p, nearPoints=p, farPoints=0, nearGames=0 if p < 4 else 1 if p < 8 else 2,
                                   farGames=0, nearSets=0, farSets=0, endChanges=0 if p <= 4 else 1,
                                   server='Near' if p <= 4 else 'Far', nearEnd='Near' if p <= 4 else 'Far',
                                   farEnd='Far' if p <= 4 else 'Near') for p in range(1, 9)]
                cases.append(dict(near=near, far=baseline.IDS[(i+1) % 15], difficulty=level, seed=seed,
                                  nearServes=4, farServes=4, nearHits=2, farHits=3, attempts=4, rejected=1,
                                  decisions=4, endChanges=1, serviceChanges=1, nearGames=2, farGames=0,
                                  points=8, physicsTicks=100, actions=[4, 0, 0, 0, 0, 0],
                                  swingResults=[5, 0, 0, 0, 0, 1, 0, 0, 0, 0], ballEvents=[8, 0, 0, 0], trajectory=trajectory))
    return dict(schema=1, passed=True, unityVersion='6000.3.26f1', modelSha256='a'*64, cases=cases)


class BaselineValidationTests(unittest.TestCase):
    def setUp(self):
        self.raw = fixture()

    def reject(self, mutate):
        mutate(self.raw)
        with self.assertRaises((ValueError, KeyError, TypeError)):
            baseline.validate(self.raw)

    def test_complete_cohort_covers_both_roles_at_each_level_and_seed(self):
        cases = baseline.validate(self.raw)
        for level in baseline.LEVELS:
            for seed in baseline.SEEDS:
                for role in ('near', 'far'):
                    self.assertEqual(set(baseline.IDS), {c[role] for c in cases if c['difficulty'] == level and c['seed'] == seed})

    def test_incomplete_run_rejected(self):
        self.reject(lambda r: r.update(passed=False))

    def test_missing_case_rejected(self):
        self.reject(lambda r: r['cases'].pop())

    def test_duplicate_case_rejected(self):
        self.reject(lambda r: r['cases'].__setitem__(1, copy.deepcopy(r['cases'][0])))

    def test_wrong_pair_rejected(self):
        self.reject(lambda r: r['cases'][0].update(far='CHN'))

    def test_missing_point_rejected(self):
        self.reject(lambda r: r['cases'][0]['trajectory'].pop())

    def test_invented_point_jump_rejected(self):
        self.reject(lambda r: r['cases'][0]['trajectory'][1].update(nearPoints=4))

    def test_duplicate_rally_rejected(self):
        self.reject(lambda r: r['cases'][0]['trajectory'][1].update(rally=1))

    def test_rejections_cannot_be_hidden(self):
        self.reject(lambda r: r['cases'][0].update(rejected=0))

    def test_action_shape_rejected(self):
        self.reject(lambda r: r['cases'][0].update(actions=[4]))

    def test_action_totals_rejected(self):
        self.reject(lambda r: r['cases'][0].update(decisions=5))

    def test_missing_service_coverage_rejected(self):
        self.reject(lambda r: r['cases'][0].update(farServes=0))

    def test_unfinished_games_rejected(self):
        self.reject(lambda r: r['cases'][0].update(nearGames=1))

    def test_no_physics_rejected(self):
        self.reject(lambda r: r['cases'][0].update(physicsTicks=0))

    def test_swapped_identity_rejected(self):
        self.reject(lambda r: r['cases'][0]['trajectory'][0].update(nearEnd='Far'))

    def test_malformed_counter_rejected(self):
        self.reject(lambda r: r['cases'][0].update(nearHits=True))

    def test_nonfinite_counter_rejected(self):
        self.reject(lambda r: r['cases'][0].update(attempts=float('nan')))

    def test_negative_counter_rejected(self):
        self.reject(lambda r: r['cases'][0].update(rejected=-1))

    def test_wrong_engine_version_rejected(self):
        self.reject(lambda r: r.update(unityVersion='6000.2.0f1'))

    def test_missing_model_hash_rejected(self):
        self.reject(lambda r: r.pop('modelSha256'))

    def test_hit_distribution_mismatch_rejected(self):
        self.reject(lambda r: r['cases'][0]['swingResults'].__setitem__(0, 6))

    def test_final_score_mismatch_rejected(self):
        self.reject(lambda r: r['cases'][0]['trajectory'][-1].update(nearGames=1))

    def test_json_duplicate_fields_and_nonfinite_values_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'invalid.json'
            for text in ('{"passed":true,"passed":false}', '{"value":NaN}', '{"value":Infinity}'):
                path.write_text(text)
                with self.assertRaises(ValueError):
                    baseline.read_json(path)

    def test_summary_preserves_rejected_actions_and_role_totals(self):
        summary = baseline.summarize(baseline.validate(self.raw))
        self.assertEqual(90, summary['overall']['rejected'])
        self.assertEqual(720, summary['overall']['points'])
        for role in ('near', 'far'):
            self.assertTrue(all(group['cases'] == 6 for group in summary['byCharacter'][role].values()))


if __name__ == '__main__':
    unittest.main()
