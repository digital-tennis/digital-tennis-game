#!/usr/bin/env python3
"""Convert Natural Earth 110m physical land to a compact Unity UI mesh (outer rings)."""
import json,sys
from pathlib import Path

def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def triangulate(points):
    remaining=list(range(len(points)));triangles=[]
    if sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1]))<0: remaining.reverse()
    while len(remaining)>3:
        found=False
        for i,b in enumerate(remaining):
            a,c=remaining[i-1],remaining[(i+1)%len(remaining)];pa,pb,pc=points[a],points[b],points[c]
            if cross(pa,pb,pc)<=1e-10:continue
            lo=(min(pa[0],pb[0],pc[0]),min(pa[1],pb[1],pc[1]));hi=(max(pa[0],pb[0],pc[0]),max(pa[1],pb[1],pc[1]))
            blocked=False
            for d in remaining:
                if d in (a,b,c):continue
                p=points[d]
                if not(lo[0]<=p[0]<=hi[0] and lo[1]<=p[1]<=hi[1]):continue
                if min(cross(pa,pb,p),cross(pb,pc,p),cross(pc,pa,p))>=-1e-10:blocked=True;break
            if blocked:continue
            triangles.extend((a,b,c));remaining.pop(i);found=True;break
        if not found:raise ValueError('Cannot triangulate land ring; inspect the source before changing the map.')
    triangles.extend(remaining);return triangles
source=Path(sys.argv[1] if len(sys.argv)>1 else '.work/frontend/ne_110m_land.geojson')
polygons=[]
for f in json.loads(source.read_text())['features']:
    g=f['geometry'];rings=[g['coordinates']] if g['type']=='Polygon' else g['coordinates']
    for poly in rings:
        ring=poly[0]
        if max(p[1] for p in ring)<-60:continue
        points=[]
        for x,y,*_ in ring[:-1]:
            p=(round((x+180)/360,6),round((y+60)/145,6))
            if not points or p!=points[-1]:points.append(p)
        if points[-1]==points[0]:points.pop()
        # Remove exact collinear vertices before ear clipping; the 110m source is already simplified.
        changed=True
        while changed and len(points)>3:
            changed=False
            for i in range(len(points)):
                if abs(cross(points[i-1],points[i],points[(i+1)%len(points)]))<1e-10:
                    points.pop(i);changed=True;break
        polygons.append({'xy':[n for p in points for n in p],'triangles':triangulate(points)})
out=Path('Assets/Resources/FrontEnd/WorldLand.json')
out.write_text(json.dumps({'polygons':polygons},separators=(',',':'))+'\n')
print('Land rings:',len(polygons),'vertices:',sum(len(p['xy'])//2 for p in polygons),'bytes:',out.stat().st_size)
