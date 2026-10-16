#!/usr/bin/env python3
"""Arrange unchanged native camera images into labeled review sheets; never renders gameplay."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args()
folder = args.source.resolve()
ids = 'KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()
records = []
for output, suffix in [('faces-sheet', '-face'), ('skills-sheet', '-phase-50')]:
    sheet = Image.new('RGB', (1600, 660), (15, 24, 31))
    draw = ImageDraw.Draw(sheet)
    for index, ident in enumerate(ids):
        path = folder / (ident + suffix + '.png')
        original = path.read_bytes()
        with Image.open(path) as source:
            assert source.format == 'PNG' and min(source.size) >= 640, path
            thumb = source.convert('RGB').resize((320, 180))
        x, y = index % 5 * 320, index // 5 * 220
        draw.text((x + 8, y + 8), ident + ' - native camera / staged preview', fill='white')
        sheet.paste(thumb, (x, y + 32))
        records.append({'sheet': output, 'source': path.name, 'sha256': hashlib.sha256(original).hexdigest()})
    sheet.save(folder / (output + '.png'))
(folder / 'sheets-sources.json').write_text(json.dumps(records, indent=2) + '\n')
print('Composed two 15-athlete sheets from unmodified native PNGs; source hashes retained.')
