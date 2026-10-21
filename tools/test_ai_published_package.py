"""Real temporary Git histories for published package provenance; no gameplay claims."""
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('training_evidence',Path(__file__).with_name('verify-ai-training.py'))
training=importlib.util.module_from_spec(spec);spec.loader.exec_module(training)


class PublishedPackageTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.repo=Path(self.tmp.name)
        self.git('init','--initial-branch=main')
        (self.repo/'observed.py').write_bytes(b'measured source\n')
        self.git('add','observed.py');self.git('commit','-m','base')
        self.expected={'observed.py':hashlib.sha256(b'measured source\n').hexdigest()}
        self.git('switch','-c','feature')
        (self.repo/'later.txt').write_text('Later feature documentation\n')
        self.git('add','later.txt');self.git('commit','-m','feature')
        self.feature=self.git('rev-parse','HEAD').strip()
        self.git('switch','main');self.git('merge','--squash','feature');self.git('commit','-m','squashed')
        self.squash=self.git('rev-parse','HEAD').strip()

    def tearDown(self):self.tmp.cleanup()

    def git(self,*args):
        return subprocess.check_output(['git','-c','user.name=Fixture','-c','user.email=fixture@example.invalid',
                                        '-c','commit.gpgsign=false',*args],cwd=self.repo,stderr=subprocess.DEVNULL,text=True)

    def test_squashed_package_accepted_without_feature_ancestry(self):
        r=subprocess.run(['git','merge-base','--is-ancestor',self.feature,self.squash],cwd=self.repo)
        self.assertEqual(r.returncode,1)
        training.published_sources(self.repo,self.squash,self.expected)

    def test_changed_published_source_is_rejected(self):
        (self.repo/'observed.py').write_bytes(b'changed after measurement\n')
        self.git('add','observed.py');self.git('commit','-m','changed')
        with self.assertRaisesRegex(AssertionError,'differs from measured source'):
            training.published_sources(self.repo,self.git('rev-parse','HEAD').strip(),self.expected)

    def test_unpinned_reference_is_rejected(self):
        with self.assertRaisesRegex(AssertionError,'full commit SHA'):
            training.published_sources(self.repo,'main',self.expected)


if __name__=='__main__':unittest.main()
