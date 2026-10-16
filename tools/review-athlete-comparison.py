#!/usr/bin/env python3
"""Build a local review index from accepted Unity captures, without changing source pixels."""
import hashlib
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / '.work/athlete-comparison'
BASELINE = '69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def generate():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = []
    sections = []

    def link(path, label, preview=False):
        path = ROOT / path
        relative = path.relative_to(ROOT).as_posix()
        records.append({'path': relative, 'sha256': digest(path)})
        url = '../../' + relative
        title = html.escape(label)
        if preview:
            return f'<figure><a href="{url}"><img loading="lazy" src="{url}" alt="{title}"></a><figcaption>{title} — 클릭하면 원본 크기</figcaption></figure>'
        return f'<a href="{url}">{title}</a>'

    for athlete in ('KOR', 'JPN'):
        sections.append(f'<h2>{athlete}: 중립 자세 전후 비교 / staged art preview</h2>')
        for view, label in [('front', '정면 전신·의상'), ('side', '측면 전신·목·팔다리'), ('face', '얼굴·헤어·칼라')]:
            sections.append(link(f'.work/proportion-review/comparison-{athlete}-{view}.png', label, True))
            for phase in ('before', 'after'):
                sections.append(link(f'.work/proportion-review/{phase}-{athlete}-{view}.png', f'{athlete} {phase} {view} 원본 PNG') + ' ')
        sections.append(f'<h2>{athlete}: 실제 CLI 어댑터 입력 경기 / after</h2>')
        for action, label in [('Running', '달리기·방향 전환'), ('Serve', '서브·회복'), ('Forehand', '포핸드·회복'), ('Backhand', '백핸드·회복')]:
            name = f'{athlete}-{action}'
            sections.append(link(f'.work/ac2-motion/{name}-sequence.png', label, True))
            timeline_path = f'.work/ac2-motion/motion/{name}-timeline.json'
            timeline = json.loads((ROOT / timeline_path).read_text())['samples']
            sections.append(link(timeline_path, '실제 시각·스윙 ID·속도 기록'))
            sections.append('<details><summary>연속 원본 16프레임 / 회복 표시</summary>')
            for sample in timeline:
                recovery = action != 'Running' and not sample['swinging'] and sample['swingAge'] >= .349
                label = f"frame {sample['frame']:02} | t={sample['clock']:.3f}s | age={sample['swingAge']:.3f}s"
                if recovery:
                    label += ' | RECOVERY / 회복'
                sections.append(link(f".work/ac2-motion/motion/{name}-{sample['frame']:02}.png", label) + '<br>')
            sections.append('</details>')
    for path in ('.work/proportion-review/before.json', '.work/proportion-review/after.json', '.work/proportion-review/before-assets.txt', '.work/proportion-review/after-assets.txt', '.work/ac2-motion/build-receipt.json', '.work/ac2-motion/review.json'):
        sections.append('<p>' + link(path, path) + '</p>')
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><title>KOR/JPN Unity 전후 비교</title>
<style>body{max-width:1800px;margin:24px auto;padding:16px;background:#131821;color:#eee;font:18px/1.6 sans-serif}a{color:#9bd4ff}img{max-width:100%;height:auto}figure{margin:24px 0}details{padding:12px;border:1px solid #526070}</style>
<h1>KOR / JPN 실제 Unity 비교</h1>
<p>정면·측면·얼굴은 같은 900×1100 카메라·조명·중립 자세입니다. BEFORE는 원본 리비전 ''' + BASELINE + '''의 메시를 Git에서 읽어 Unity로 재현했습니다. 원본 보관 PNG를 덮어쓰지 않습니다. 비교표는 원본 픽셀을 그대로 붙이고 바깥에만 라벨을 더합니다. 브라우저 축소 표시와 별개로 링크는 원본 해상도를 엽니다.</p>
<p>중립 자세는 staged art preview입니다. 동작은 이번 작업 트리의 새 검증 Mac 앱에서 정상 입력 소스 → 라우터 → 어댑터를 거친 실제 경기입니다. 이전 버전 경기 동작의 matched 전후 영상이라고 주장하지 않습니다. 동작 시트는 0/4/8/15 프레임이며 나머지 16장 모두 열 수 있습니다. 마지막 회복은 실제 스윙 종료 값으로 표시합니다. 자동 재생·오디오는 없습니다.</p>
<p>남은 한계: 로우폴리 얼굴·헤어의 각진 면, 천 시뮬레이션 없는 의상, 정지 손가락 그립, 큰 런지의 발 앵커 보정. 테니스 모캡은 아니며 취득한 전신 이동 트랙에 authored 타격·IK를 결합했습니다. Windows·BLE·두 PC 검증 자료가 아닙니다.</p>
''' + '\n'.join(sections) + '</html>\n'
    (OUTPUT / 'index.html').write_text(page)
    (OUTPUT / 'manifest.json').write_text(json.dumps({'baselineRevision': BASELINE, 'artEvidence': 'staged matched Unity cameras', 'motionEvidence': 'actual native CLI adapter match, after only', 'files': records}, indent=2) + '\n')
    print(f'Generated {OUTPUT.relative_to(ROOT)}/index.html with {len(records)} hashed references; source PNGs unchanged.')


if __name__ == '__main__':
    generate()
