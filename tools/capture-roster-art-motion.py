#!/usr/bin/env python3
"""Run independent, muted, GPU-camera-only validation matches for the complete roster."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
PAIRS=['KOR-JPN','CHN-USA','RUS-IND','DEU-GBR','ESP-FRA','AUS-NLD','BRA-MAR','ZAF-KOR']

def capture(pair):
    out=ROOT/'.work/roster-motion'/pair
    command=[sys.executable,'tools/verify-polish.py','--player',
             'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis',
             '--only','motion','--out',str(out),'--motion-pair',pair.replace('-',',')]
    subprocess.run(command,cwd=ROOT,check=True)
    report=json.loads((out/'motion/motion.json').read_text())
    assert report['nearId']+'-'+report['farId']==pair and report['passed'],pair
    print('ROSTER_MOTION_PASS '+pair,flush=True)

if __name__=='__main__':
    # Each match has its own CSV overrides, metrics, audio copies and camera folder.
    # Two independent players are safe on the established same-Mac GPU validation path.
    with ThreadPoolExecutor(max_workers=2) as executor:list(executor.map(capture,PAIRS))
    print('All fifteen actual roster skins captured through ordinary adapter inputs',flush=True)
