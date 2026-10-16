#!/usr/bin/env python3
"""Fail-closed receipt for actual Unity XML, Mac camera frames and listener WAVs.

Does not create gameplay, screenshots, audio, test outcomes or human review claims.
Run --begin after building and before native runs. All native artifacts must be newer.
"""
import argparse
import hashlib
import html
import json
import subprocess
import time
import wave
import xml.etree.ElementTree as ET
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
IDS = 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def runtime(build=None):
    matches = list(((build or ROOT / 'Builds/mac') / 'DigitalTennis.app').rglob('DigitalTennis.Runtime.dll'))
    assert len(matches) == 1, 'Build Mac first; expected one runtime assembly'
    return matches[0]


def inspect(root):
    receipt = json.loads((root / 'run.json').read_text())
    build_dir = (ROOT / receipt.get('buildDirectory', 'Builds/mac')).resolve()
    build_dir.relative_to(ROOT)
    assert digest(runtime(build_dir)) == receipt['runtimeSha256'], 'Build changed during evidence run'
    build_path = build_dir / 'build-info.json'
    assert digest(build_path) == receipt['buildInfoSha256'], 'Build manifest changed during evidence run'
    build = json.loads(build_path.read_text())
    assert build['unity'] == '6000.3.26f1' and build['platform'] == 'StandaloneOSX'
    assert 'Assets/Scenes/FrontEnd.unity' in build['scenes']
    artifacts = []

    def required(relative):
        path = root / relative
        assert path.is_file(), f'Missing native evidence: {relative}'
        assert path.stat().st_mtime_ns >= receipt['startedNs'], f'Stale native evidence: {relative}'
        artifacts.append({'path': str(path.relative_to(ROOT)), 'sha256': digest(path)})
        return path

    def report(relative):
        return json.loads(required(relative).read_text())

    def png(relative):
        with Image.open(required(relative)) as image:
            assert image.format == 'PNG' and min(image.size) >= 640, relative
            values = np.asarray(image.convert('RGB'), dtype=float)
            assert values.std() > 12 and len(np.unique(values[::10, ::10].reshape(-1, 3), axis=0)) > 50, f'Blank frame: {relative}'

    def wav(relative):
        with wave.open(str(required(relative))) as stream:
            assert stream.getsampwidth() == 2 and stream.getframerate() >= 22050, relative
            values = np.frombuffer(stream.readframes(stream.getnframes()), dtype='<i2').astype(float) / 32768
            assert len(values) / stream.getframerate() / stream.getnchannels() > .025, relative
            assert np.sqrt(np.mean(values ** 2)) > .0001, f'Silent listener output: {relative}'
            assert np.max(np.abs(values)) < 1, f'Clipped output: {relative}'

    for relative in ['kor-jpn/roster/player.log', 'all/roster/player.log', 'all/motion/player.log',
                     'all/intro/player.log', 'front/frontend.log', 'audio/fresh/player.log',
                     'audio/existing/player.log', 'online/host.log', 'online/guest.log']:
        log = required(relative).read_text()
        assert 'Initialize engine version: 6000.3.26f1' in log and 'Initializing Metal device caps:' in log, relative

    tests = []
    for name in ['test-edit', 'test-play', 'test-motion', 'test-front']:
        path = ROOT / '.work' / (name + '.xml')
        xml = ET.parse(path).getroot()
        assert xml.get('result') == 'Passed' and int(xml.get('failed')) == 0 and int(xml.get('passed')) > 0, name
        assert len(xml.findall('.//test-case[@result="Passed"]')) == int(xml.get('passed')), name
        tests.append({'command': 'bash tools/unity.sh ' + name, 'passed': int(xml.get('passed')), 'sha256': digest(path)})
    for subset, ids in [('kor-jpn', IDS[:2]), ('all', IDS)]:
        roster = report(subset + '/roster/result.json')
        assert roster['characters'] == len(ids) and roster['runtimeErrors'] == 0 and roster['points'] == 0
        for ident in ids:
            actor = report(f'{subset}/roster/{ident}.json')
            assert actor['profile']['id'] == ident and actor['mode'] == 'staged-character-and-skill-preview'
            for suffix in ['', '-face', '-detail', '-phase-18', '-phase-50', '-phase-80']:
                png(f'{subset}/roster/{ident}{suffix}.png')
    png('all/roster/faces-sheet.png')
    png('all/roster/skills-sheet.png')
    motion = report('all/motion/motion.json')
    assert motion['passed'] and motion['errors'] == 0 and motion['strokes'] == 6 and motion['contacts'] >= 26 and motion['serves'] >= 6
    assert motion['capturedServe'] and motion['capturedRunning']
    assert motion['maxGripError'] < .035 and motion['maxContactError'] < .035 and motion['maxPlantedError'] < .04 and motion['maxPlantedSlipPerFrame'] < .035
    for name in ['Serve', 'Running', 'Forehand', 'Backhand', 'Volley', 'Slice', 'Lob', 'Smash']:
        png('all/motion/' + name + '.png')
    intro = report('all/intro/result.json')
    assert intro['passed'] and intro['audioRecorded'] and intro['listenerSamples'] > 0 and intro['runtimeErrors'] == 0
    assert intro['ticksBefore'] == intro['ticksAfter'] and intro['points'] == 0 and len(intro['shots']) >= 8
    assert len(intro['times']) == intro['frames'] and intro['frames'] > 30
    for index in range(intro['frames']):
        png(f'all/intro/frame-{index:04}.png')
    wav('all/intro/intro-listener.wav')
    movie = required('opening-camera-preview.mp4')
    streams = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams']
    assert {'video', 'audio'} <= {stream['codec_type'] for stream in streams}, 'Opening review must include actual listener audio'
    assert all(float(stream['duration']) > 5 for stream in streams if stream['codec_type'] in ['video', 'audio'])
    front = report('front/result.json')
    assert front['passed'] and front['runtimeErrors'] == 0 and front['selectableCharacters'] == 15 and front['pauseHeld'] and front['rematchReset']
    for name in front['captures']:
        report('front/' + name + '.json')
        png('front/' + name + '.png')
    for mode, count in [('fresh', 60), ('existing', 8)]:
        prefix = 'audio/' + mode + '/'
        assert report(prefix + 'result.json')['passed']
        upgrade = report(prefix + 'upgrade-result.json')
        audio = report(prefix + 'audio-result.json')
        assert upgrade['monitorMuted'], 'Offscreen validation must not emit speaker audio'
        assert upgrade['nativePlatform'] == 'OSXPlayer' and upgrade['graphics'] == 'Metal'
        assert len(upgrade['voices']) == count and min(upgrade[k] for k in ['steps', 'skids', 'contacts']) > 0
        assert audio['passed'] and audio['musicMuted'] and audio['musicVolume'] == 0 and audio['pointCheers'] == 1 and audio['crowdStoppedAtNextServe']
        for voice in upgrade['voices']:
            wav(prefix + voice['cue'] + '-playback.wav')
        for ident in IDS if mode == 'fresh' else IDS[:2]:
            png(prefix + ident + '-speech-audition.png')
        png(prefix + 'live-rally.png')
        wav(prefix + 'game-mix.wav')
    audit = report('audio/waveform-audit.json')
    assert len(audit) == 68 and min(row['correlation'] for row in audit) > .8
    online = report('online/combined.json')
    assert online['rendering'] == 'offscreen-camera' and online['relayedMessages'] > 0
    assert len({p['pid'] for p in online['processes']}) == 2 and all(p['code'] == 0 for p in online['processes'])
    host, guest = online['host'], online['guest']
    assert host['score'] == guest['score'] and host['winner'] == guest['winner']
    assert host['physicsTicks'] > 0 and guest['physicsTicks'] == 0 and host['epoch'] == guest['epoch'] and host['epoch'] >= 2
    assert guest['view']['worldPoseChecks'] > 0 and guest['view']['strokeChecks'] > 0
    assert guest['view']['worldPoseErrors'] == 0 and guest['view']['strokeErrors'] == 0
    assert guest['characterChecks'] > 30 and guest['characterErrors'] == 0
    for role in ['host', 'guest']:
        for path in sorted((root / 'online').glob(role + '-view*.png')):
            png(str(path.relative_to(root)))
    assert any(a['path'].endswith('guest-view-skill.png') for a in artifacts), 'Missing buffered guest skill frame'
    result = {'platform': 'native macOS / Metal', 'build': build, 'runtimeSha256': receipt['runtimeSha256'], 'tests': tests,
              'motion': motion, 'artifacts': artifacts,
              'scope': 'Automated adapter/UI callbacks and local two-process WebSocket relay; staged roster skill stock is visual only. No physical keyboard, BLE, external IMU, Windows or two-PC claim.'}
    (root / 'verified.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    parts = ['<!doctype html><meta charset="utf-8"><title>Native athlete review</title>',
             '<style>body{background:#16212a;color:#eee;font:18px system-ui;margin:24px}img{max-width:32%;vertical-align:top}audio{display:block;width:80%}a{color:#9de}h2{margin-top:40px}</style>',
             '<h1>Native Mac Unity evidence</h1><p>' + html.escape(result['scope']) + '</p>',
             '<p>Original game-camera PNGs and AudioListener WAVs. English Flite synthesized speech with Korean subtitles. No actors or physical-input claim.</p>']
    for subset, ids in [('kor-jpn', IDS[:2]), ('all', IDS)]:
        parts.append('<h2>' + subset + ' staged art / skill review (granted stock)</h2>')
        for ident in ids:
            parts.append('<h3>' + ident + '</h3>')
            for suffix in ['-face', '-detail', '-phase-18', '-phase-50', '-phase-80']:
                src = f'{subset}/roster/{ident}{suffix}.png'
                parts.append(f'<a href="{src}"><img loading="lazy" alt="{ident}{suffix}" src="{src}"></a>')
    parts.append('<h2>Real adapter-driven serve / running / contacts</h2>')
    for name in ['Serve', 'Running', 'Forehand', 'Backhand', 'Volley', 'Slice', 'Lob', 'Smash']:
        src = 'all/motion/' + name + '.png'
        parts.append(f'<a href="{src}"><img alt="{name}" src="{src}"></a>')
    parts.append('<h2>Actual listener audio</h2>')
    for src in ['all/intro/intro-listener.wav', 'audio/fresh/game-mix.wav', 'audio/existing/game-mix.wav'] + [f'audio/fresh/{ident}.{cue}-playback.wav' for ident in IDS for cue in ['ready', 'skill', 'win', 'lose']]:
        parts.append(f'<p>{src}</p><audio controls preload="none" src="{src}"></audio>')
    parts.append('<h2>Same-Mac host / buffered guest</h2>')
    for role in ['host', 'guest']:
        src = 'online/' + role + '-view-skill.png'
        parts.append(f'<a href="{src}"><img alt="{role} skill" src="{src}"></a>')
    (root / 'review.html').write_text('\n'.join(parts) + '\n')
    print(f'PASS actual Unity XML and {len(artifacts)} fresh native artifacts, all 15 athletes, six strokes, grip/feet, intro/game audio and buffered guest')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--begin', action='store_true')
    parser.add_argument('--build', type=Path, default=ROOT / 'Builds/mac')
    args = parser.parse_args()
    root = args.out.resolve()
    root.relative_to(ROOT)
    if args.begin:
        root.mkdir(parents=True, exist_ok=True)
        build_dir = args.build.resolve()
        build_dir.relative_to(ROOT)
        (root / 'run.json').write_text(json.dumps({'startedNs': time.time_ns(), 'buildDirectory': str(build_dir.relative_to(ROOT)), 'runtimeSha256': digest(runtime(build_dir)),
                                                'buildInfoSha256': digest(build_dir / 'build-info.json')}, indent=2) + '\n')
        print('Native evidence run opened for current Mac runtime; earlier artifacts will be rejected.')
    else:
        inspect(root)


if __name__ == '__main__':
    main()
