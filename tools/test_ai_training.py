"""Validate the actual evidence receipt and exercise rejection of corrupted copies."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('verify_ai_training',ROOT/'tools/verify-ai-training.py');verify=importlib.util.module_from_spec(spec);spec.loader.exec_module(verify)
class EvidenceTests(unittest.TestCase):
 def test_actual_authoritative_training_evidence(self):verify.verify()
 def test_reject_false_budget_completion_weights_and_integrity_claims(self):
  original=verify.ROOT
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);folder=root/'docs/evidence/ai-full-match';shutil.copytree(original/'docs/evidence/ai-full-match',folder)
   path=folder/'training.json';real=json.loads(path.read_text())
   try:
    verify.ROOT=root
    for key,value in [('passed',False),('steps',65535),('rolloutUpdates',3),('matches',1),('finalWeightsSha256',real['initialWeightsSha256']),('rawSha256','0'*64),('unityTestsSha256','0'*64),('games',real['games']+1),('sets',real['sets']+1)]:
     with self.subTest(key=key):
      candidate=dict(real);candidate[key]=value;path.write_text(json.dumps(candidate))
      with self.assertRaises((AssertionError,FileNotFoundError)):verify.verify()
   finally:verify.ROOT=original
if __name__=='__main__':unittest.main()
