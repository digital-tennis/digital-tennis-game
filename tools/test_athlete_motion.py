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
  folder=ROOT/'.work/ac2-motion/motion';r=json.loads((folder/'motion.json').read_text())
  self.assertTrue(r['passed']);self.assertEqual(r['errors'],0);self.assertLessEqual(r['maxArmStretch'],1.081)
  for metric in ['maxContactError','maxGripError','maxPlantedSlipPerFrame','maxPlantedError']:self.assertLess(r[metric],.035)
  from PIL import Image,ImageStat
  for athlete in ['KOR','JPN']:
   for action in ['Serve','Running','Forehand','Backhand']:
    for frame in [0,4,8,15]:
     p=folder/f'{athlete}-{action}-{frame:02}.png'
     self.assertGreater(p.stat().st_size,20000)
     with Image.open(p) as image:self.assertGreater(max(ImageStat.Stat(image).stddev),20)
  with (ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Info.plist').open('rb') as stream:
   self.assertEqual(plistlib.load(stream)['CFBundleIdentifier'],'com.digitaltennis.validation')
  receipt=json.loads((ROOT/'.work/ac2-motion/build-receipt.json').read_text())
  dll=ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Resources/Data/Managed/DigitalTennis.Runtime.dll'
  self.assertEqual(receipt['runtimeSha256'],hashlib.sha256(dll.read_bytes()).hexdigest())
  for entry in receipt['sources']:self.assertEqual(entry['sha256'],hashlib.sha256((ROOT/entry['path']).read_bytes()).hexdigest())
  for entry in receipt['playerFiles']:self.assertEqual(entry['sha256'],hashlib.sha256((ROOT/entry['path']).read_bytes()).hexdigest())
  resources=(ROOT/'Builds/verification/mac/DigitalTennis.app/Contents/Resources/Data/resources.assets').read_bytes()
  for athlete in ['KOR','JPN']:
   self.assertIn(receipt['meshNames'][athlete].encode(),resources)
   self.assertIn((ROOT/f'Assets/Resources/Characters/Acquired/Body{athlete}.json').read_bytes(),resources)
  self.assertGreater((folder/'motion.json').stat().st_mtime,receipt['buildTime'])
  for p in folder.glob('*.png'):self.assertGreater(p.stat().st_mtime,receipt['buildTime'])
  for athlete in ['KOR','JPN']:
   timeline=json.loads((folder/f'{athlete}-Forehand-timeline.json').read_text())['samples']
   self.assertLess(timeline[0]['swingAge'],.06)
   self.assertTrue(any(s['swingAge']>=.349 and not s['swinging'] for s in timeline),'Must actually record recovery')
 def test_fresh_buffered_online_motion(self):
  folder=ROOT/'.work/ac2-online';report=json.loads((folder/'combined.json').read_text());host=report['host'];guest=report['guest']
  self.assertEqual(guest['physicsTicks'],0);self.assertGreater(host['physicsTicks'],0)
  self.assertEqual(host['score'],guest['score']);self.assertGreaterEqual(guest['epoch'],2)
  self.assertGreater(guest['view']['worldPoseChecks'],0);self.assertGreater(guest['view']['strokeChecks'],0)
  self.assertEqual(guest['view']['worldPoseErrors'],0);self.assertEqual(guest['view']['strokeErrors'],0)
  self.assertEqual([p['index'] for p in guest['characters']],[0,1])
  receipt=json.loads((ROOT/'.work/ac2-motion/build-receipt.json').read_text())
  self.assertGreater((folder/'combined.json').stat().st_mtime,receipt['buildTime'])
  for role in ['host','guest']:
   self.assertGreater((folder/(role+'-view.png')).stat().st_mtime,receipt['buildTime'])
if __name__=='__main__':unittest.main()
