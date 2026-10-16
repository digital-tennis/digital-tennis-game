"""Live, read-only delivery checks. Requires authenticated gh and a pushed draft PR.

Run explicitly after delivery; this does not build Unity or replay native evidence.
"""
import json
from pathlib import Path
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "digital-tennis/digital-tennis-game"


def command(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True, timeout=60).strip()


class AthleteDeliveryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.head = command("git", "rev-parse", "HEAD")
        cls.branch = command("git", "branch", "--show-current")
        cls.pr = json.loads(command(
            "gh", "pr", "view", cls.branch, "--repo", REPOSITORY, "--json",
            "url,state,isDraft,baseRefName,headRefName,headRefOid,body"))

    def test_open_draft_targets_main_at_local_commit(self):
        self.assertEqual(self.pr["state"], "OPEN")
        self.assertTrue(self.pr["isDraft"])
        self.assertEqual(self.pr["baseRefName"], "main")
        self.assertEqual(self.pr["headRefName"], self.branch)
        self.assertEqual(self.pr["headRefOid"], self.head)
        self.assertTrue(self.pr["url"].startswith(f"https://github.com/{REPOSITORY}/pull/"))

    def test_remote_branch_and_upstream_match_local_commit(self):
        remote = command("git", "ls-remote", "--exit-code", "origin", f"refs/heads/{self.branch}")
        self.assertEqual(remote.split()[0], self.head)
        self.assertEqual(command("git", "rev-parse", "@{upstream}"), self.head)

    def test_implementation_and_delivery_sources_are_committed(self):
        paths = [
            "Assets/Resources/Athletes/KOR.asset", "Assets/Resources/Athletes/JPN.asset",
            "Assets/Resources/Characters/Acquired/BodyKOR.json",
            "Assets/Resources/Characters/Acquired/BodyJPN.json",
            "Assets/Resources/Characters/Acquired/FullBodyMotion.json",
            "Assets/Scripts/Runtime/AcquiredAthleteMotion.cs",
            "Assets/ThirdParty/Quaternius/archive-inspection.json",
            "docs/athlete-pr-delivery.md", "tools/test_athlete_delivery.py",
        ]
        for path in paths:
            with self.subTest(path=path):
                self.assertTrue((ROOT / path).is_file())
                command("git", "cat-file", "-e", f"HEAD:{path}")
        # .gitattributes requests LF, but PR18 stores these two originals as CRLF.
        # Keep their original bytes and exclude only Git's normalization-only status.
        preserved = ("Assets/StreamingAssets/Balance/characters.csv", "docs/audio-audition-plan.csv")
        for path in preserved:
            original = subprocess.check_output(["git", "show", f"HEAD:{path}"], cwd=ROOT, timeout=60)
            self.assertEqual((ROOT / path).read_bytes(), original)
        status = command("git", "status", "--porcelain", "--", "Assets", "tools", "docs")
        for line in status.splitlines():
            self.assertIn(line[3:] if line.startswith(" ") else line[2:], preserved)

    def test_no_generated_evidence_or_machine_state_committed(self):
        paths = command("git", "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD").splitlines()
        self.assertTrue(paths)
        excluded = ("Library/", "Temp/", "UserSettings/", "Builds/", ".work/", ".ouroboros/", ".vscode/")
        self.assertFalse([p for p in paths if p.startswith(excluded) or p.endswith((".csproj", ".sln"))])

    def test_pr_explains_asset_scope_evidence_and_limits(self):
        for phrase in ("CC0", "Idle_Loop", "Jog_Fwd_Loop", "staged art preview",
                       ".work/athlete-comparison/index.html", "169", "37", "Windows", "BLE"):
            with self.subTest(phrase=phrase):
                self.assertIn(phrase, self.pr["body"])


if __name__ == "__main__":
    unittest.main()
