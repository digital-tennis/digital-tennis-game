"""Audit recorded Unity evidence and reject corrupted gate/reward/terminal records."""
import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SPEC=importlib.util.spec_from_file_location('verify_self_play',Path(__file__).with_name('verify-ai-self-play.py'))
v=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(v)

class RecordedSelfPlay(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.report=json.loads((v.FOLDER/'self-play.json').read_text())
        cls.lines=gzip.decompress((v.FOLDER/cls.report['rawFile']).read_bytes()).decode().splitlines()
    def test_actual_unity_self_play_and_held_out_receipts(self):
        v.verify()
    def corrupted(self,predicate,mutate):
        lines=self.lines.copy();changed=False
        for i,line in enumerate(lines):
            e=json.loads(line)
            if predicate(e):mutate(e);lines[i]=json.dumps(e);changed=True;break
        self.assertTrue(changed)
        with tempfile.TemporaryDirectory() as d:
            folder=Path(d);raw=folder/'raw.jsonl';raw.write_text('\n'.join(lines)+'\n')
            report=copy.deepcopy(self.report);report['rawFile']=raw.name;report['rawSha256']=hashlib.sha256(raw.read_bytes()).hexdigest()
            with patch.object(v,'FOLDER',folder),self.assertRaises(AssertionError):
                v.trajectory(report,raw.name,report['ppoConfig'])
    def test_rejects_unaccounted_far_hit(self):
        self.corrupted(lambda e:e['state']['farAttempts']>0,lambda e:e['state'].__setitem__('farHits',e['state']['farHits']+1))
    def test_rejects_reset_without_true_terminal(self):
        self.corrupted(lambda e:e['state']['done'],lambda e:e['state'].__setitem__('done',False))
    def test_rejects_artificial_reward(self):
        self.corrupted(lambda e:e['kind']=='transition',lambda e:e['state'].__setitem__('reward',1000))
    def test_rejects_point_jump(self):
        self.corrupted(lambda e:e['kind']=='transition',lambda e:e['state'].__setitem__('nearPoints',2))
    def test_rejects_invalid_action(self):
        self.corrupted(lambda e:e['kind']=='transition',lambda e:e['state'].__setitem__('farAction',6))
if __name__=='__main__':unittest.main()
