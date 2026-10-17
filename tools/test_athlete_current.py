"""Current KOR/JPN upgrade acceptance suite; reads retained real Unity evidence."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

from test_athlete_proportions import AthleteProportionTests
from test_athlete_provenance import AthleteProvenanceTests
from test_athlete_regression import AthleteRegressionTests
from test_athlete_motion import AthleteMotionTests
from test_athlete_comparison import AthleteComparisonTests
from test_athlete_delivery import AthleteDeliveryTests

ROOT = Path(__file__).resolve().parents[1]


class UnityRunReceiptTests(unittest.TestCase):
    def test_observed_unity_editmode_and_playmode_results(self):
        for filename, minimum in [('test-edit.xml', 169), ('test-play.xml', 37)]:
            with self.subTest(filename=filename):
                report = ET.parse(ROOT / '.work' / filename).getroot()
                self.assertEqual(report.get('result'), 'Passed')
                self.assertGreaterEqual(int(report.get('total', '0')), minimum)
                self.assertEqual(int(report.get('failed', '-1')), 0)
                self.assertEqual(int(report.get('skipped', '0')), 0)


if __name__ == '__main__':
    unittest.main()
