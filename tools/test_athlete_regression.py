"""Preservation checks against the preserved KOR/JPN baseline source, alongside Unity tests.

These checks intentionally compare shipped data, not generated native evidence.
Run with the project-local verification Python; no reports or user files are written.
"""
import csv
import hashlib
import io
import json
from pathlib import Path
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[1]
BASELINE = "b3fcbed1188a38982dca4833f6417074d1d6a618"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def original(path):
    return git("show", f"{BASELINE}:{path}")


class AthleteRegressionTests(unittest.TestCase):
    def test_all_character_gameplay_and_match_rules_values_are_preserved(self):
        for filename in ("characters.csv", "gameplay.csv", "rules.csv", "gestures.csv"):
            path = f"Assets/StreamingAssets/Balance/{filename}"
            with self.subTest(path=path):
                before = list(csv.reader(io.StringIO(original(path).decode("utf-8-sig"))))
                after = list(csv.reader(io.StringIO((ROOT / path).read_text(encoding="utf-8-sig"))))
                self.assertEqual(after, before)

    def test_all_fifteen_personas_identities_ages_and_team_colors_are_preserved(self):
        path = "Assets/Resources/Characters/roster.json"
        before = json.loads(original(path))
        after = json.loads((ROOT / path).read_text())
        self.assertEqual(after, before)
        athletes = {p["id"]: p for p in after["characters"]}
        self.assertEqual(len(athletes), 15)
        self.assertEqual((athletes["KOR"]["name"], athletes["KOR"]["age"]), ("Han Seo-yun", 26))
        self.assertEqual((athletes["JPN"]["name"], athletes["JPN"]["age"]), ("Ren Aoyama", 24))

    def test_other_thirteen_keep_authored_identity_while_acquiring_full_bodies(self):
        path = "Assets/Resources/Characters/appearance.json"
        before = {p["id"]: p for p in json.loads(original(path))["styles"]}
        after = {p["id"]: p for p in json.loads((ROOT / path).read_text())["styles"]}
        self.assertEqual(set(after), set(before))
        others = set(before) - {"KOR", "JPN"}
        self.assertEqual(len(others), 13)
        for athlete in sorted(others):
            with self.subTest(athlete=athlete):
                self.assertEqual(after[athlete]['acquiredBody'],'Body'+athlete)
                self.assertIn('garment',after[athlete])
                for key,value in before[athlete].items():
                    if key=='acquiredHair' and athlete=='MAR':continue  # fitted scalp under original coiled-bun identity
                    self.assertEqual(after[athlete][key],value,key)
        for athlete in ('KOR','JPN'):
            self.assertEqual(before[athlete],after[athlete])
            path='Assets/Resources/Athletes/'+athlete+'.asset'
            self.assertEqual((ROOT/path).read_bytes(),original(path))

    def test_shipped_sfx_synthetic_english_voices_and_korean_subtitles_are_byte_preserved(self):
        entries = git("ls-tree", "-r", BASELINE, "Assets/StreamingAssets/Audio").decode().splitlines()
        self.assertTrue(entries, "Baseline audio tree must exist")
        for entry in entries:
            metadata, path = entry.split("\t", 1)
            _, kind, expected = metadata.split()
            self.assertEqual(kind, "blob")
            with self.subTest(path=path):
                content = (ROOT / path).read_bytes()
                # Git's blob digest checks all source WAVs, captions and provenance,
                # without launching playback or relying on retained audit reports.
                actual = hashlib.sha1(b"blob " + str(len(content)).encode() + b"\0" + content).hexdigest()
                self.assertEqual(actual, expected)


if __name__ == "__main__":
    unittest.main()
