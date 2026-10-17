#!/usr/bin/env python3
"""Replay PR18 animation with the same opt-in camera recorder in a disposable clone.

Only CLI verification instrumentation and the validation app identifier are backported.
No athlete/model/animation/gameplay production sources are changed in the baseline.
"""
import argparse, hashlib, json, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = '69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0'
PROJECT = ROOT / '.work/baseline-motion-project'
OUT = ROOT / '.work/baseline-motion'
DRIVER = 'Assets/Scripts/Runtime/MotionValidationDriver.cs'
SETUP = 'Assets/Scripts/Editor/ProjectSetup.cs'
EDITOR = '/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity'


def run(*args, cwd=ROOT):
    subprocess.run(args, cwd=cwd, check=True)


def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def prepare():
    if PROJECT.exists():
        raise SystemExit('Baseline clone already exists; retain prior evidence or move it explicitly first')
    OUT.mkdir(parents=True, exist_ok=True)
    run('git', 'clone', '--shared', '--no-checkout', str(ROOT), str(PROJECT))
    run('git', 'checkout', '--detach', BASE, cwd=PROJECT)
    text = (ROOT / DRIVER).read_text()
    text = text.replace('maxSlip,maxGrip,maxArmStretch;', 'maxSlip,maxGrip;')
    text = text.replace('                maxArmStretch=Mathf.Max(maxArmStretch,rig.ArmStretch);\n', '')
    text = text.replace('&&maxArmStretch<=AcquiredAthleteMotion.Config.maxArmStretch+.001f', '')
    text = text.replace(',maxArmStretch=maxArmStretch', '')
    text = text.replace(',maxGripError,maxArmStretch;', ',maxGripError;')
    text = text.replace('rig.GripPosition', 'hand.position')
    text = text.replace('speed=rig.LocomotionSpeed,armStretch=rig.ArmStretch', 'speed=samples.Count==0?0:Vector3.Distance(actor.transform.position,samples[samples.Count-1].root)/Mathf.Max(.0001f,Time.realtimeSinceStartup-samples[samples.Count-1].clock)')
    text = text.replace('speed,armStretch;', 'speed;')
    assert 'AcquiredAthleteMotion' not in text and 'rig.ArmStretch' not in text
    (PROJECT / DRIVER).write_text(text)
    setup = (PROJECT / SETUP).read_text()
    assert 'com.digitaltennis.validation' in setup
    (PROJECT / SETUP).write_text(setup.replace('com.digitaltennis.validation', 'com.digitaltennis.baseline'))
    entries = subprocess.check_output(['git', 'ls-tree', '-r', BASE, 'Assets'], cwd=ROOT, text=True).splitlines()
    records = []
    for entry in entries:
        metadata, name = entry.split('\t', 1)
        if name in (DRIVER, SETUP):
            continue
        expected = metadata.split()[2]
        content = (PROJECT / name).read_bytes()
        actual = hashlib.sha1(b'blob ' + str(len(content)).encode() + b'\0' + content).hexdigest()
        assert actual == expected, name
        records.append({'path': name, 'sha256': hashlib.sha256(content).hexdigest()})
    receipt = {'baselineRevision': BASE, 'productionFiles': records,
               'instrumentation': [{'path': n, 'sha256': digest(PROJECT / n)} for n in (DRIVER, SETUP)],
               'scope': 'Original PR18 production assets/scripts. Only CLI recorder and unique baseline app ID are backported. Same live camera/framing as final; independent real games, not identical ball trajectories.'}
    (OUT / 'source-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('BASELINE_PRODUCTION_BYTES_VERIFIED', len(records), flush=True)


def build():
    run(EDITOR, '-batchmode', '-quit', '-projectPath', str(PROJECT), '-executeMethod',
        'DigitalTennis.Editor.ProjectSetup.BuildVerificationMac', '-logFile', str(OUT / 'build.log'))
    player = PROJECT / 'Builds/verification/mac/DigitalTennis.app'
    receipt = json.loads((OUT / 'source-receipt.json').read_text())
    receipt['build'] = [{'path': str(p.relative_to(PROJECT)), 'sha256': digest(p)} for p in [
        player / 'Contents/Info.plist', player / 'Contents/Resources/Data/Managed/DigitalTennis.Runtime.dll',
        player / 'Contents/Resources/Data/resources.assets', PROJECT / 'Builds/verification/mac/build-info.json']]
    receipt['buildTime'] = (PROJECT / 'Builds/verification/mac/build-info.json').stat().st_mtime
    (OUT / 'source-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('BASELINE_BUILD_READY', flush=True)


def capture():
    player = PROJECT / 'Builds/verification/mac/DigitalTennis.app/Contents/MacOS/Digital Tennis'
    run(sys.executable, 'tools/verify-polish.py', '--player', str(player), '--only', 'motion', '--out', str(OUT))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['prepare', 'build', 'capture'])
    action = parser.parse_args().action
    {'prepare': prepare, 'build': build, 'capture': capture}[action]()
