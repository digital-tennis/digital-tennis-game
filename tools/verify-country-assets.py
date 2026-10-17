#!/usr/bin/env python3
"""Verify retained country sources, license and derived resources without a network dependency."""
import hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];base=ROOT/'Assets/ThirdParty/Selection'
r=json.loads((base/'provenance.json').read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert 'MIT License' in (base/'FlagIcons/LICENSE.txt').read_text()
expected='KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split();assert [f['id'] for f in r['flags']]==expected
for flag in r['flags']:
 assert sha(base/'FlagIcons'/(flag['id']+'.svg'))==flag['sourceSha256']
 png=ROOT/flag['png'];assert sha(png)==flag['pngSha256'];assert struct.unpack('>II',png.read_bytes()[16:24])==(512,384)
source=base/'ne_110m_admin_0_countries.geojson';assert sha(source)==r['mapSourceSha256']
assert 'public domain' in (base/'NaturalEarth-Terms.html').read_text()
regions=[]
for f in json.loads(source.read_text())['features']:
 ident=f['properties']['ADM0_A3'];g=f['geometry'];polys=[g['coordinates']] if g['type']=='Polygon' else g['coordinates']
 for poly in polys:
  if max(p[1] for p in poly[0]) < -60:continue
  rings=[dict(hole=i>0,xy=[round(v,6) for x,y,*_ in ring[:-1] for v in ((x+180)/360,(y+60)/145)]) for i,ring in enumerate(poly)]
  regions.append(dict(id=ident,rings=rings))
output=ROOT/'Assets/Resources/FrontEnd/WorldCountries.json'
assert json.loads(output.read_text())==dict(regions=regions);assert sha(output)==r['mapOutputSha256']
assert set(expected)<={region['id'] for region in regions}
print('PASS:15 flag PNG/SVG pairs, original MIT license, pinned Natural Earth source/public-domain terms, exact country-ring conversion and roster coverage')
