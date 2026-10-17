"""Inspect actual acquired full skeleton, retarget output and fresh runtime evidence."""
import hashlib,json,plistlib,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class AthleteMotionTests(unittest.TestCase):
 def test_retained_source_and_generated_tracks(self):
  manifest=json.loads((ROOT/'Assets/ThirdParty/Quaternius/motion-bindings.json').read_text())
  self.assertEqual(manifest['license'],'CC0-1.0')
  for entry in manifest['files']:self.assertEqual(hashlib.sha256((ROOT/entry['path']).read_bytes()).hexdigest(),entry['sha256'])
  data=json.loads((ROOT/'Assets/Resources/Characters/Acquired/FullBodyMotion.json').read_text())
  for clip in data['clips']:
   self.assertGreater(len(clip['frames']),15)
   self.assertEqual(len(clip['frames'][0]['rotations']),10)
   for f in clip['frames']:
    for q in f['rotations']:self.assertAlmostEqual(sum(v*v for v in q.values()),1,places=5)
  self.assertEqual([c['name'] for c in data['clips']],['Idle_Loop','Walk_Loop','Jog_Fwd_Loop','Sprint_Loop'])
 def test_fresh_native_motion(self):
  # Full roster guard also requires real displacement/stride, not cached speed.
  from test_roster_art import RosterArtEvidenceTests
  case=RosterArtEvidenceTests()
  case.test_every_player_has_live_running_serve_contact_and_recovery_samples()
  case.test_packaged_player_contains_all_fifteen_final_meshes_and_bodies()
  with (ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Info.plist').open('rb') as stream:
   self.assertEqual(plistlib.load(stream)['CFBundleIdentifier'],'com.digitaltennis.validation')
 def test_fresh_buffered_online_motion(self):
  folder=ROOT/'.work/roster-online';report=json.loads((folder/'combined.json').read_text());host=report['host'];guest=report['guest']
  self.assertEqual(guest['physicsTicks'],0);self.assertGreater(host['physicsTicks'],0)
  self.assertEqual(host['score'],guest['score']);self.assertGreaterEqual(guest['epoch'],2)
  self.assertGreater(guest['view']['worldPoseChecks'],0);self.assertGreater(guest['view']['strokeChecks'],0)
  self.assertEqual(guest['view']['worldPoseErrors'],0);self.assertEqual(guest['view']['strokeErrors'],0)
  self.assertEqual([p['index'] for p in guest['characters']],[0,1])
  build_time=(ROOT/'Builds/verification/mac/build-info.json').stat().st_mtime
  self.assertGreater((folder/'combined.json').stat().st_mtime,build_time)
  for role in ['host','guest']:
   self.assertGreater((folder/(role+'-view.png')).stat().st_mtime,build_time)
if __name__=='__main__':unittest.main()
