"""Matched real roster evidence: original bytes/pixels and independently played motion."""
import hashlib,json,re,subprocess,unittest
from pathlib import Path
from PIL import Image,ImageChops
from test_roster_art import PAIRS,IDS,assert_running
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'.work/roster-art-review'
BASE='b3fcbed1188a38982dca4833f6417074d1d6a618'

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

class AthleteComparisonTests(unittest.TestCase):
 def assert_pixels(self,a,b):
  self.assertEqual(a.size,b.size)
  self.assertIsNone(ImageChops.difference(a.convert('RGB'),b.convert('RGB')).getbbox())
 def test_baseline_meshes_and_matched_camera_receipts(self):
  for ident in IDS:
   source=subprocess.check_output(['git','show',f'{BASE}:Assets/Resources/Athletes/{ident}.asset'],cwd=ROOT)
   self.assertEqual((ART/'originals'/f'{ident}.asset').read_bytes(),source)
   self.assertIn(hashlib.sha256(source).hexdigest(),(ART/'before-assets.txt').read_text())
   self.assertIn(sha(ROOT/f'Assets/Resources/Athletes/{ident}.asset'),(ART/'after-assets.txt').read_text())
  before=json.loads((ART/'before.json').read_text());after=json.loads((ART/'after.json').read_text())
  self.assertEqual(before,after);self.assertEqual(before['baselineRevision'],BASE)
  self.assertNotIn('Null',before['graphics']);self.assertTrue(before['stagedArtPreview'])
 def test_comparison_sheets_preserve_every_source_pixel(self):
  for ident in IDS:
   for view in ('front','side','face'):
    with Image.open(ART/f'comparison-{ident}-{view}.png') as sheet:
     self.assertEqual(sheet.size,(1800,1160))
     for phase,x in [('before',0),('after',900)]:
      with Image.open(ART/f'{phase}-{ident}-{view}.png') as source:self.assert_pixels(source,sheet.crop((x,60,x+900,1160)))
 def test_original_baseline_runtime_and_production_sources(self):
  folder=ROOT/'.work/roster-baseline';project=ROOT/'.work/roster-baseline-project'
  receipt=json.loads((folder/'source-receipt.json').read_text());self.assertEqual(receipt['baselineRevision'],BASE)
  entries=subprocess.check_output(['git','ls-tree','-r',BASE,'Assets'],cwd=ROOT,text=True).splitlines()
  originals={e.split('\t',1)[1]:e.split('\t',1)[0].split()[2] for e in entries}
  self.assertEqual(set(originals)-{e['path'] for e in receipt['instrumentation']},{e['path'] for e in receipt['productionFiles']})
  for record in receipt['productionFiles']:
   content=(project/record['path']).read_bytes()
   self.assertEqual(hashlib.sha256(content).hexdigest(),record['sha256'])
   self.assertEqual(hashlib.sha1(b'blob '+str(len(content)).encode()+b'\0'+content).hexdigest(),originals[record['path']])
  for record in receipt['build']:self.assertEqual(sha(project/record['path']),record['sha256'])
  # The original gameplay/animation is untouched; only the shared observer and bundle ID differ.
  self.assertEqual((project/'Assets/Scripts/Runtime/MotionValidationDriver.cs').read_bytes(),(ROOT/'Assets/Scripts/Runtime/MotionValidationDriver.cs').read_bytes())
  import plistlib
  with (project/'Builds/verification/mac/DigitalTennis.app/Contents/Info.plist').open('rb') as f:self.assertEqual(plistlib.load(f)['CFBundleIdentifier'],'com.digitaltennis.baseline')
  for pair in PAIRS:
   d=folder/pair/'motion';r=json.loads((d/'motion.json').read_text())
   self.assertEqual(r['errors'],0);self.assertGreaterEqual(r['contacts'],26)
   self.assertGreater((d/'motion.json').stat().st_mtime,receipt['buildTime'])
   for ident in pair.split('-'):assert_running(json.loads((d/f'{ident}-Running-timeline.json').read_text())['samples'])
 def test_motion_sheets_preserve_both_live_frame_pixels_and_recovery(self):
  for pair in PAIRS:
   for ident in pair.split('-'):
    for action in ('Running','Serve','Forehand','Backhand'):
     sheet=Image.open(ART/'motion-comparison'/pair/f'{ident}-{action}.png')
     self.assertEqual(sheet.size,(2400,3760))
     for column,source in enumerate([ROOT/'.work/roster-baseline'/pair/'motion',ROOT/'.work/roster-motion'/pair/'motion']):
      timeline=json.loads((source/f'{ident}-{action}-timeline.json').read_text())['samples']
      self.assertEqual([s['frame'] for s in timeline],list(range(16)))
      self.assertTrue(all(a['clock']<b['clock'] for a,b in zip(timeline,timeline[1:])))
      if action=='Running':assert_running(timeline)
      else:
       self.assertTrue(timeline[0]['swinging']);self.assertLess(timeline[0]['swingAge'],.12)
       self.assertTrue(any(not s['swinging'] and s['swingAge']>=.349 for s in timeline))
      for row,frame in enumerate((0,4,8,15)):
       with Image.open(source/f'{ident}-{action}-{frame:02}.png') as original:self.assert_pixels(original,sheet.crop((column*1200,row*940+40,column*1200+1200,row*940+940)))
     sheet.close()
 def test_review_links_hashes_and_honest_labels(self):
  page=(ART/'index.html').read_text();receipt=json.loads((ART/'review.json').read_text())
  self.assertEqual(receipt['baselineRevision'],BASE)
  for entry in receipt['images']:self.assertEqual(sha(ART/entry['path']),entry['sha256'])
  for url in re.findall(r'(?:href|src)="([^"]+)"',page):self.assertTrue((ART/url).is_file(),url)
  for phrase in ['staged neutral art','independent matches','RECOVERY','low-poly','baseline quality']:self.assertIn(phrase,page)
  self.assertNotIn('autoplay',page)

if __name__=='__main__':unittest.main()
