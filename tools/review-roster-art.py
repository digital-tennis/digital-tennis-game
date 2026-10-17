#!/usr/bin/env python3
"""Compose labelled sheets from unretouched Unity cameras; bind originals to source hashes."""
import argparse
import hashlib
import html
import json
from pathlib import Path
import shutil
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.work/roster-art-review'
IDS='KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
PAIRS=['KOR-JPN','CHN-USA','RUS-IND','DEU-GBR','ESP-FRA','AUS-NLD','BRA-MAR','ZAF-KOR']

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--motion',action='store_true')
    parser.add_argument('--publish',type=Path)
    parser.add_argument('--reuse-composites',action='store_true',help='Refresh receipts/pages only after verifying unchanged production and retained pixels')
    args=parser.parse_args()
    if args.reuse_composites:
        prior=json.loads((OUT/'review.json').read_text())
        for entry in prior['images']:
            assert sha(OUT/entry['path'])==entry['sha256'],entry['path']
        for entry in prior['sources']:
            if entry['path'].startswith(('Assets/','Packages/','ProjectSettings/')):
                assert sha(ROOT/entry['path'])==entry['sha256'],entry['path']
    records=[]
    pages=['<!doctype html><meta charset="utf-8"><title>Roster athlete art review</title>',
           '<style>body{background:#131822;color:white;font:18px system-ui;margin:25px}img{max-width:100%}a{color:#9de}section{margin-top:45px}</style>',
           '<h1>Roster athlete art review</h1><p>Unity 6000.3.26f1 / Metal. Staged neutral art is separate from live adapter-driven matches. Original camera pixels are linked and retained unchanged. Sheets only add labels outside those pixels. No audio playback.</p>',
           '<p>BEFORE: b3fcbed1188a38982dca4833f6417074d1d6a618 meshes. Identical front/side/face orthographic cameras and lighting. KOR/JPN preserved. Low-poly faceting is intentional.</p>']
    for ident in IDS:
        pages.append('<section><h2>'+ident+'</h2>')
        for view in ['front','side','face']:
            sheet=Image.new('RGB',(1800,1160),'#131822');draw=ImageDraw.Draw(sheet)
            for x,phase in [(0,'before'),(900,'after')]:
                source=OUT/f'{phase}-{ident}-{view}.png'
                with Image.open(source) as original:
                    assert original.size==(900,1100)
                    sheet.paste(original,(x,60))
                draw.text((x+20,15),f'{ident} {view} | {phase.upper()} | staged neutral art',fill='white',font_size=24)
                records.append(dict(path=source.name,sha256=sha(source)))
            name=f'comparison-{ident}-{view}.png'
            if not(args.reuse_composites and (OUT/name).is_file()):sheet.save(OUT/name)
            pages.append(f'<a href="{name}"><img loading="lazy" alt="{ident} {view}" src="{name}"></a>')
            pages.append(f'<p><a href="before-{ident}-{view}.png">BEFORE original</a> · <a href="after-{ident}-{view}.png">AFTER original</a></p>')
        pages.append('</section>')
    if args.motion:
        pages.append('<h1>Actual running / serve / accepted contacts / recovery</h1><p>Each pair plays an independent match with its unchanged balance. Camera follows the actor with the same relative parameters. These are sampled live serves, not a claim that ball trajectories match across players.</p>')
        for pair in PAIRS:
            source=ROOT/'.work/roster-motion'/pair/'motion'
            report=json.loads((source/'motion.json').read_text())
            assert report['passed'] and report['nearId']+'-'+report['farId']==pair
            dest=OUT/'motion'/pair;dest.mkdir(parents=True,exist_ok=True)
            for path in sorted(source.glob('*')):
                if path.suffix not in ('.png','.json','.log'):continue
                shutil.copyfile(path,dest/path.name)
                records.append(dict(path=str((dest/path.name).relative_to(OUT)),sha256=sha(dest/path.name)))
            for ident in pair.split('-'):
                pages.append('<h2>'+ident+' live motion ('+pair+')</h2>')
                for action in ['Running','Serve','Forehand','Backhand']:
                    timeline=json.loads((dest/f'{ident}-{action}-timeline.json').read_text())['samples']
                    board=Image.new('RGB',(2400,1880),'#131822');draw=ImageDraw.Draw(board)
                    for k,i in enumerate([0,4,8,15]):
                        original=dest/f'{ident}-{action}-{i:02}.png'
                        with Image.open(original) as image:board.paste(image,(k%2*1200,k//2*940+40))
                        sample=timeline[i]
                        draw.text((k%2*1200+10,k//2*940+8),f"{ident} {action} sample {i} | swing {sample['swingId']} age {sample['swingAge']:.3f}s",font_size=22,fill='white')
                    name=f'{ident}-{action}-sequence.png'
                    if not(args.reuse_composites and (dest/name).is_file()):board.save(dest/name)
                    relative=str((dest/name).relative_to(OUT))
                    pages.append(f'<a href="{relative}"><img loading="lazy" alt="{ident} {action}" src="{relative}"></a>')
                    pages.append('<details><summary>16 unretouched original camera frames</summary>')
                    for frame in range(16):
                        relative=str((dest/f'{ident}-{action}-{frame:02}.png').relative_to(OUT))
                        pages.append(f'<a href="{relative}"><img style="width:24%" loading="lazy" alt="{ident} {action} {frame}" src="{relative}"></a>')
                    pages.append('</details>')
    if args.motion:
        pages.append('<h1>Matched before / after real motion</h1><p>Original b3fcbed baseline quality is reported honestly, including any failed anatomical guards. These are independent matches with the same recorder/camera, not identical trajectories. Contact, FOLLOW-THROUGH and RECOVERY are linked separately from staged neutral art. The low-poly style and static finger grip remain visible.</p>')
        for pair in PAIRS:
            old=ROOT/'.work/roster-baseline'/pair/'motion'
            dest=OUT/'baseline'/pair;dest.mkdir(parents=True,exist_ok=True)
            for path in sorted(old.glob('*')):
                if path.suffix not in ('.png','.json','.log'):continue
                shutil.copyfile(path,dest/path.name)
                records.append(dict(path=str((dest/path.name).relative_to(OUT)),sha256=sha(dest/path.name)))
            report=json.loads((old/'motion.json').read_text())
            pages.append(f'<h2>{pair}: original baseline quality guards passed={report["passed"]}</h2>')
            comp=OUT/'motion-comparison'/pair;comp.mkdir(parents=True,exist_ok=True)
            for ident in pair.split('-'):
                for action in ['Running','Serve','Forehand','Backhand']:
                    board=Image.new('RGB',(2400,3760),'#131822');draw=ImageDraw.Draw(board)
                    for column,(phase,folder) in enumerate([('BEFORE',dest),('AFTER',OUT/'motion'/pair)]):
                        timeline=json.loads((folder/f'{ident}-{action}-timeline.json').read_text())['samples']
                        for row,frame in enumerate([0,4,8,15]):
                            source=folder/f'{ident}-{action}-{frame:02}.png'
                            with Image.open(source) as im:board.paste(im,(column*1200,row*940+40))
                            draw.text((column*1200+12,row*940+8),f'{phase} {ident} {action} {frame} age={timeline[frame]["swingAge"]:.3f}s',font_size=22,fill='white')
                    name=f'{ident}-{action}.png'
                    if not(args.reuse_composites and (comp/name).is_file()):board.save(comp/name)
                    relative=str((comp/name).relative_to(OUT));records.append(dict(path=relative,sha256=sha(comp/name)))
                    pages.append(f'<details><summary>{ident} {action} contact to RECOVERY: BEFORE left / AFTER right</summary><a href="{relative}"><img loading="lazy" src="{relative}"></a></details>')
        src=ROOT/'.work/roster-baseline/source-receipt.json';shutil.copyfile(src,OUT/'baseline/source-receipt.json')
        records.append(dict(path='baseline/source-receipt.json',sha256=sha(src)))
    make_previews()
    sources=[p for folder in ['Assets','Packages','ProjectSettings'] for p in (ROOT/folder).rglob('*') if p.is_file()]
    sources += [p for folder in ['tools','docs'] for p in (ROOT/folder).rglob('*')
                if p.is_file() and p.suffix in ('.py','.sh','.mjs','.toml','.txt','.md') and '__pycache__' not in p.parts]
    sources += [ROOT/'README.md',ROOT/'AGENTS.md']
    receipt=dict(baselineRevision='b3fcbed1188a38982dca4833f6417074d1d6a618',stagedArtSeparateFromLiveMatches=True,
                 images=records,sources=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in sorted(sources)])
    if args.motion:
        build=ROOT/'Builds/verification/mac'
        receipt['player']=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in sorted(build.rglob('*')) if p.is_file()]
    if args.motion:
        verification=OUT/'verification';verification.mkdir(exist_ok=True)
        reports=[ROOT/'.work'/name for name in ['test-edit.xml','test-play.xml',
                 'logs/check.log','logs/build-verification-mac.log']]
        online=ROOT/'.work/roster-online'
        reports += [p for p in [online/'combined.json',online/'host-view.png',online/'guest-view.png',ROOT/'.work/regression-receipt.json'] if p.is_file()]
        receipt['reports']=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p)) for p in reports]
        pages.append('<h2>Actual Unity test/build evidence</h2>')
        for path in reports:
            shutil.copyfile(path,verification/path.name)
            pages.append(f'<p><a href="verification/{path.name}">{path.name}</a></p>')
    (OUT/'review.json').write_text(json.dumps(receipt,indent=2)+'\n')
    (OUT/'index.html').write_text('\n'.join(pages)+'\n')
    if args.publish:
        dest=args.publish.resolve()/'review'
        shutil.copytree(OUT,dest,dirs_exist_ok=True,ignore=shutil.ignore_patterns('originals','live-review-*.png','*-sheet.png'))
        print('Portable gallery: '+str(dest/'index.html'))
    print('Verified and composed '+str(len(records))+' original Unity artifacts')

def make_previews():
    # Clearly labelled downsampled overviews for the PR; original pixels remain in the gallery.
    dest=ROOT/'docs/images';dest.mkdir(parents=True,exist_ok=True)
    targets=IDS[2:]
    for view,label in [('front','body'),('face','face')]:
        board=Image.new('RGB',(1600,7*510),'#131822');draw=ImageDraw.Draw(board)
        for i,ident in enumerate(targets):
            x,y=(i%2)*800,(i//2)*510
            draw.text((x+15,y+8),f'{ident}  BEFORE | AFTER (thumbnail overview)',font_size=20,fill='white')
            for n,phase in enumerate(['before','after']):
                with Image.open(OUT/f'{phase}-{ident}-{view}.png') as im:board.paste(im.resize((380,464)),(x+n*400,y+40))
        board.save(dest/f'roster-{label}-comparison.png',optimize=True)
    board=Image.new('RGB',(1600,7*360),'#131822');draw=ImageDraw.Draw(board)
    for i,ident in enumerate(targets):
        pair=next(p for p in PAIRS if ident in p.split('-'));x,y=i%2*800,i//2*360
        draw.text((x+12,y+8),f'{ident} FOREHAND: BEFORE | AFTER (independent matches)',font_size=18,fill='white')
        for n,folder in enumerate([OUT/'baseline'/pair,OUT/'motion'/pair]):
            with Image.open(folder/f'{ident}-Forehand-04.png') as im:board.paste(im.resize((400,300)),(x+n*400,y+40))
    board.save(dest/'roster-motion-comparison.png',optimize=True)

if __name__=='__main__':main()
