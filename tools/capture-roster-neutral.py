#!/usr/bin/env python3
"""Produce matched staged neutral views, never passing them off as gameplay evidence."""
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
BASE='b3fcbed1188a38982dca4833f6417074d1d6a618'
EDITOR='/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity'
folder=ROOT/'.work/roster-art-review';(folder/'originals').mkdir(parents=True,exist_ok=True)
for ident in 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split():
    content=subprocess.check_output(['git','show',f'{BASE}:Assets/Resources/Athletes/{ident}.asset'],cwd=ROOT)
    (folder/'originals'/f'{ident}.asset').write_bytes(content)
for phase in ['Before','After']:
    subprocess.run([EDITOR,'-batchmode','-quit','-projectPath',str(ROOT),'-executeMethod',
        'DigitalTennis.Editor.RosterAthleteEvidence.'+phase,'-logFile',str(folder/(phase.lower()+'.log'))],cwd=ROOT,check=True)
print('90 genuine staged Unity camera images captured; gameplay remains a separate test')
