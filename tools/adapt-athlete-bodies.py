#!/usr/bin/env python3
"""Bake inspected CC0 full-body topology into the existing contact rig's rest space.
No gameplay or animation data is modified. See body-retarget.json for authored mapping.
"""
import hashlib, importlib.util, json
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('acquired',ROOT/'tools/adapt-athlete-assets.py')
mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
CFG=ROOT/'Assets/Resources/Characters/body-retarget.json'

def warped_height(height, profile):
    """Preserve the source crown above the body knots instead of flattening it."""
    source, target = profile['sourceHeights'], profile['targetHeights']
    if height > source[-1]:
        slope = (target[-1] - target[-2]) / (source[-1] - source[-2])
        return target[-1] + (height - source[-1]) * slope
    return float(np.interp(height, source, target))


def bake(profile):
    path=ROOT/profile['source'];g=mod.Gltf(path);world={}
    def visit(i,parent):
        n=g.g['nodes'][i];w=parent@g.matrix(n);world[n['name']]=w
        for c in n.get('children',[]):visit(c,w)
    for i in g.g['scenes'][0]['nodes']:visit(i,np.eye(4))
    joints=[g.g['nodes'][i]['name'] for i in g.g['skins'][0]['joints']]
    mappings=profile['joints'];rows=[];tri=[];textures=set()
    def mapping(name):
        if name in mappings:return mappings[name]
        if any(x in name for x in ['index','middle','ring','pinky','thumb']):return mappings['hand_'+name[-1]]
        if name.startswith('ball'):return mappings['foot_'+name[-1]]
        return mappings['pelvis']
    def transform(v,name):
        entry=mapping(name);origin=world[entry['source']][:3,3];local=v-origin
        if not entry.get('arm'):
            # A single continuous warp preserves shared torso/neck/head topology across joint blends.
            return np.array([v[0],warped_height(v[1], profile),v[2]*profile['depthScale']+.015])
        if entry.get('arm'):
            # T pose lateral axis becomes the rig's downward arm axis; preserve forward depth.
            sign=1 if name.endswith('_l') else -1
            local=np.array([sign*local[1],-sign*local[0]*entry['lengthScale'],local[2]])
        at=local+entry['target']
        # Right racket hand only: curl the acquired finger topology around the
        # authored grip cylinder; preserve wrist and palm vertices continuously.
        if name.endswith('_l') and any(x in name for x in ['index','middle','ring','pinky','thumb']):
            grip=profile['grip'];distance=max(0,grip['fingerBase']-at[1]);angle=min(grip['curlMax'],distance/grip['curlRadius'])
            at[1]=grip['fingerBase']-grip['curlRadius']*np.sin(angle) if distance>0 else at[1]
            at[2]+=grip['curlRadius']*(1-np.cos(angle))
        return at
    for part,verts in g.parts():
        attrs=part['attributes'];js=g.acc(attrs['JOINTS_0']);ws=g.acc(attrs['WEIGHTS_0']);uv=g.acc(attrs['TEXCOORD_0'])
        mat=g.g['materials'][part['material']];kind=0 if 'Superhero' in mat['name'] else 1 if 'Eyes' in mat['name'] else 2
        image=None
        tex=mat['pbrMetallicRoughness'].get('baseColorTexture')
        if tex:
            uri=g.g['images'][g.g['textures'][tex['index']]['source']]['uri'];textures.add(path.parent/uri);image=mod.Image.open(path.parent/uri).convert('RGB')
        start=len(rows)
        for i,v in enumerate(verts):
            weights={};at=np.zeros(3)
            for j,w in zip(js[i],ws[i]):
                if w<=0:continue
                name=joints[j];entry=mapping(name);at+=transform(v,name)*w;bone=entry['bone'];weights[bone]=weights.get(bone,0)+float(w)
            ordered=sorted(weights.items(),key=lambda p:-p[1]);total=sum(weights.values())
            color=[1.,1.,1.]
            if image:
                u,vv=uv[i];color=[c/255 for c in image.getpixel((int(u%1*(image.width-1)),int((1-vv%1)*(image.height-1))))]
            rows.append(dict(x=float(at[0]),y=float(at[1]),z=float(at[2]),r=color[0],g=color[1],b=color[2],kind=kind,
                             bones=[b for b,w in ordered],weights=[w/total for b,w in ordered]))
        tri.extend((g.acc(part['indices']).flatten()+start).tolist())
    # Same source coordinate frame and head transform as the complete face, no independent head stretch.
    hairpath=ROOT/profile['hair'];hair=mod.Gltf(hairpath)
    for part,verts in hair.parts():
        start=len(rows)
        for v in verts:
            at=transform(v,'Head')
            rows.append(dict(x=float(at[0]),y=float(at[1]),z=float(at[2]),r=0,g=0,b=0,kind=3,bones=[2],weights=[1.]))
        tri.extend((hair.acc(part['indices']).flatten()+start).tolist())
    # Shoe lasts replace the bare toe triangles; ankle surfaces remain under the sock cuff.
    tri=[i for t in np.array(tri).reshape(-1,3) if not all(rows[int(j)]['y']<profile['shoeCut'] for j in t) for i in map(int,t)]
    used=sorted(set(tri));remap={old:new for new,old in enumerate(used)}
    rows=[rows[i] for i in used];tri=[remap[i] for i in tri]
    out=ROOT/'Assets/Resources/Characters/Acquired'/('Body'+profile['id']+'.json')
    out.write_text(json.dumps(dict(vertices=rows,triangles=tri),separators=(',',':'))+'\n')
    sources=[path,path.with_suffix('.bin'),hairpath,hairpath.with_suffix('.bin'),CFG,ROOT/'Assets/ThirdParty/Quaternius/0/License_Standard.txt']+sorted(textures)
    return dict(id=profile['id'],output=str(out.relative_to(ROOT)),sha256=mod.sha(out.read_bytes()),vertices=len(rows),triangles=len(tri)//3,
                sources=[dict(path=str(p.relative_to(ROOT)),sha256=mod.sha(p.read_bytes())) for p in sources])

if __name__=='__main__':
    config=json.loads(CFG.read_text());outputs=[bake(p) for p in config['profiles']]
    (ROOT/'Assets/ThirdParty/Quaternius/body-bindings.json').write_text(json.dumps(dict(license='CC0-1.0',provenance='provenance.json',method='tools/adapt-athlete-bodies.py',outputs=outputs),indent=2)+'\n')
    print('Baked full-body, eyes, eyebrows and fitted hair for KOR/JPN from retained CC0 sources')
