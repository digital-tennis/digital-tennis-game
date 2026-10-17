"""Current AC1: actual production GPU views and ordinary-input motion across all 15 skins."""
import hashlib
import json
import math
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from PIL import Image
from test_athlete_proportions import AthleteProportionTests
from test_athlete_regression import AthleteRegressionTests

ROOT=Path(__file__).resolve().parents[1]
PAIRS=['KOR-JPN','CHN-USA','RUS-IND','DEU-GBR','ESP-FRA','AUS-NLD','BRA-MAR','ZAF-KOR']
IDS=set('KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split())

def assert_running(samples):
    steps=[math.dist(tuple(a['root'][k] for k in ('x','y','z')),tuple(b['root'][k] for k in ('x','y','z'))) for a,b in zip(samples,samples[1:])]
    assert max(steps)<.5, 'Reset/teleport is not running'
    assert sum(steps)>=.15, 'A decaying speed with a stationary root is not running'
    assert samples[-1]['stride']-samples[0]['stride']>=.1, 'Stride must advance with real movement'

class RunningEvidenceGuardTests(unittest.TestCase):
    def test_stationary_cached_speed_is_rejected(self):
        samples=[dict(root=dict(x=0,y=0,z=0),speed=3-i*.1,stride=0) for i in range(16)]
        with self.assertRaisesRegex(AssertionError,'stationary root'):assert_running(samples)

class RosterArtEvidenceTests(unittest.TestCase):
    def test_retained_gallery_binds_current_sources_player_and_original_pixels(self):
        receipt=json.loads((ROOT/'.work/roster-art-review/review.json').read_text())
        self.assertEqual(receipt['baselineRevision'],'b3fcbed1188a38982dca4833f6417074d1d6a618')
        for entry in receipt['sources']+receipt['player']+receipt['reports']:
            self.assertEqual(hashlib.sha256((ROOT/entry['path']).read_bytes()).hexdigest(),entry['sha256'],entry['path'])
        for entry in receipt['images']:
            self.assertEqual(hashlib.sha256((ROOT/'.work/roster-art-review'/entry['path']).read_bytes()).hexdigest(),entry['sha256'])
        self.assertGreater(len(receipt['images']),1000)

    def test_packaged_player_contains_all_fifteen_final_meshes_and_bodies(self):
        import re
        build=ROOT/'Builds/verification/mac'
        runtime=next(build.rglob('DigitalTennis.Runtime.dll'))
        for path in (ROOT/'Assets/Scripts/Runtime').glob('*.cs'):
            self.assertGreater(runtime.stat().st_mtime,path.stat().st_mtime,path.name+' newer than compiled player')
        resources=(build/'DigitalTennis.app/Contents/Resources/Data/resources.assets').read_bytes()
        for ident in IDS:
            body=ROOT/f'Assets/Resources/Characters/Acquired/Body{ident}.json'
            mesh=ROOT/f'Assets/Resources/Athletes/{ident}.asset'
            name=re.search(r'  m_Name: (.+)',mesh.read_text()).group(1)
            self.assertIn(name.encode(),resources,ident+' final mesh absent from player')
            self.assertIn(body.read_bytes(),resources,ident+' final body absent from player')

    def test_full_project_tests_ran_after_final_meshes(self):
        from datetime import datetime, timezone
        changed=max(p.stat().st_mtime for p in [*(ROOT/'Assets/Resources/Athletes').glob('*.asset'),*(ROOT/'Assets/Scripts').rglob('*.cs')])
        for name,count in [('test-edit',169),('test-play',38)]:
            xml=ET.parse(ROOT/'.work'/(name+'.xml')).getroot()
            self.assertEqual(xml.get('result'),'Passed')
            self.assertGreaterEqual(int(xml.get('passed')),count)
            self.assertEqual(int(xml.get('failed')),0)
            self.assertEqual(int(xml.get('skipped')),0)
            stamp=datetime.fromisoformat(xml.get('start-time').replace(' ','T').replace('Z','+00:00'))
            started=(stamp if stamp.tzinfo else stamp.replace(tzinfo=timezone.utc)).timestamp()
            self.assertGreater(started,changed)

    def test_every_player_has_live_running_serve_contact_and_recovery_samples(self):
        seen=set()
        for pair in PAIRS:
            folder=ROOT/'.work/roster-motion'/pair/'motion'
            report=json.loads((folder/'motion.json').read_text())
            self.assertTrue(report['passed'],pair)
            self.assertEqual(report['nearId']+'-'+report['farId'],pair)
            self.assertEqual(report['errors'],0)
            self.assertEqual(report['strokes'],6)
            self.assertGreaterEqual(report['contacts'],26)
            self.assertGreaterEqual(report['serves'],6)
            for key,limit in [('maxArmStretch',1.081),('maxGripError',.035),('maxContactError',.035),('maxPlantedError',.04),('maxPlantedSlipPerFrame',.035)]:
                self.assertLessEqual(report[key],limit,pair+' '+key)
            log=(folder/'player.log').read_text()
            self.assertIn('Initialize engine version: 6000.3.26f1',log)
            self.assertIn('Initializing Metal device caps:',log)
            for ident in pair.split('-'):
                seen.add(ident)
                for action in ['Running','Serve','Forehand','Backhand']:
                    samples=json.loads((folder/f'{ident}-{action}-timeline.json').read_text())['samples']
                    self.assertEqual(len(samples),16)
                    self.assertTrue(all(a['clock']<b['clock'] for a,b in zip(samples,samples[1:])))
                    if action=='Running': assert_running(samples)
                    if action in ('Forehand','Backhand'):
                        self.assertEqual(samples[0]['stroke'],action)
                        self.assertGreaterEqual(samples[-1]['swingAge'],.349,'Must include completed post-contact recovery')
                        self.assertFalse(samples[-1]['swinging'])
                    for i in range(16):
                        frame=folder/f'{ident}-{action}-{i:02}.png'
                        self.assertGreater(frame.stat().st_mtime,(ROOT/'Builds/verification/mac/build-info.json').stat().st_mtime)
                        with Image.open(frame) as image:
                            self.assertEqual(image.size,(1200,900))
                            self.assertGreater(len(image.resize((120,90)).getcolors(10800)),100)
        self.assertEqual(seen,IDS)

if __name__=='__main__':unittest.main()
