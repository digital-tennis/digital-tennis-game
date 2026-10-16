#!/usr/bin/env python3
"""Reproduce the CC0 geometry/idle extraction from the two official FREE archives.
Requires numpy and Pillow. Archives are kept outside Assets, never imported as demos.
"""
import argparse, hashlib, json, struct, zipfile
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'Assets/ThirdParty/Quaternius'
OUT=ROOT/'Assets/Resources/Characters/Acquired'

def sha(b): return hashlib.sha256(b).hexdigest()
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,separators=(',',':'))+'\n')

class Gltf:
    def __init__(self,path):
        self.path=path;b=path.read_bytes()
        if path.suffix=='.glb':
            n=struct.unpack_from('<I',b,12)[0];self.g=json.loads(b[20:20+n]);self.buffers=[b[28+n:]]
        else:
            self.g=json.loads(b);self.buffers=[(path.parent/x['uri']).read_bytes() for x in self.g['buffers']]
    def acc(self,index):
        a=self.g['accessors'][index];v=self.g['bufferViews'][a['bufferView']];dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        return np.ndarray((a['count'],n),dtype=dtype,buffer=self.buffers[v.get('buffer',0)],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',np.dtype(dtype).itemsize*n),np.dtype(dtype).itemsize)).copy()
    def matrix(self,n):
        if 'matrix' in n:return np.array(n['matrix']).reshape(4,4).T
        x,y,z,w=n.get('rotation',[0,0,0,1]);r=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])
        m=np.eye(4);m[:3,:3]=r@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);return m
    def parts(self):
        def visit(i,parent):
            n=self.g['nodes'][i];m=parent@self.matrix(n)
            if 'mesh' in n:
                for p in self.g['meshes'][n['mesh']]['primitives']:
                    v=self.acc(p['attributes']['POSITION']);v=(m@np.c_[v,np.ones(len(v))].T).T[:,:3]
                    yield p,v
            for child in n.get('children',[]):yield from visit(child,m)
        for i in self.g['scenes'][self.g.get('scene',0)]['nodes']:yield from visit(i,np.eye(4))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--archives',type=Path,default=ROOT/'.work/athlete-assets');args=ap.parse_args()
    packs=[]
    for title,slug,folder in [('Universal Base Characters','universal-base-characters','base'),('Universal Animation Library','universal-animation-library','animation')]:
        archive=args.archives/(title+'[Standard].zip');z=zipfile.ZipFile(archive);z.extractall(args.archives/folder)
        packs.append({'title':title,'edition':'Standard FREE','source':'https://quaternius.itch.io/'+slug,'official':'https://quaternius.com/packs/'+slug.replace('-','')+'.html','license':'CC0-1.0','archive':archive.name,'archiveSha256':sha(archive.read_bytes()),'archiveFiles':len(z.namelist())})
    base=next((args.archives/'base').iterdir());anim=next((args.archives/'animation').iterdir());sources=[]
    def retain(p,pack):
        rel=p.relative_to(base if pack==0 else anim);target=DEST/str(pack)/rel;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(p.read_bytes());sources.append({'path':str(target.relative_to(ROOT)),'archiveMember':(base.name if pack==0 else anim.name)+'/'+str(rel),'sha256':sha(p.read_bytes()),'pack':pack});return target
    retain(base/'License_Standard.txt',0);retain(anim/'License.txt',1)
    for sex in ['Female','Male']:
        path=base/'Base Characters/Godot - UE'/('Superhero_'+sex+'_FullBody.gltf');g=Gltf(path);retain(path,0);retain(path.with_suffix('.bin'),0)
        # Only the acquired head/neck/eyes are used: established exact hand/foot anchors remain authored.
        rows=[];tri=[];cut=1.48 if sex=='Female' else 1.53;top=1.7666439 if sex=='Female' else 1.8100767
        for part,verts in g.parts():
            uv=g.acc(part['attributes']['TEXCOORD_0']);material=g.g['materials'][part['material']];tex=material['pbrMetallicRoughness'].get('baseColorTexture');im=None
            if tex:
                uri=g.g['images'][g.g['textures'][tex['index']]['source']]['uri'];p=path.parent/uri;im=Image.open(p).convert('RGB');
                if not any(x['path']==str((DEST/'0'/p.relative_to(base)).relative_to(ROOT)) for x in sources):retain(p,0)
            indices=g.acc(part['indices']).reshape(-1,3);indices=indices[np.all(verts[indices,1]>=cut+.06/.335*(top-cut),axis=1)];used=sorted(set(indices.flat));mapping={old:len(rows)+i for i,old in enumerate(used)}
            kind=0 if 'Superhero' in material['name'] else 1 if 'Eyes' in material['name'] else 2
            for i in used:
                x,y,z=verts[i];color=[1.,1.,1.]
                if im:
                    u,v=uv[i];color=[c/255 for c in im.getpixel((int(u%1*(im.width-1)),int((1-v%1)*(im.height-1))))]
                rows.append({'x':float(x),'y':float(1.40+(y-cut)/(top-cut)*.335),'z':float(z*1.15),'r':color[0],'g':color[1],'b':color[2],'kind':kind})
            tri.extend(mapping[int(i)] for i in indices.flat)
        # Normalize width to an appearance-catalog-controlled dimension; keep actual facial topology.
        width=max(abs(r['x']) for r in rows)*2
        for r in rows:r['x']/=width
        write(OUT/('Head'+sex+'.json'),{'vertices':rows,'triangles':tri})
    for name in ['Hair_Long','Hair_SimpleParted','Hair_Buns','Hair_Buzzed','Hair_BuzzedFemale']:
        path=base/'Hairstyles/Origin at 0/glTF (Godot)'/(name+'.gltf');g=Gltf(path);retain(path,0);retain(path.with_suffix('.bin'),0);rows=[];tri=[]
        for part,verts in g.parts():
            start=len(rows)
            for x,y,z in verts:rows.append({'x':float(x),'y':float(y),'z':float(z)})
            tri.extend((g.acc(part['indices']).flatten()+start).tolist())
        write(OUT/(name+'.json'),{'vertices':rows,'triangles':tri})
    path=anim/'Unreal-Godot/UAL1_Standard.glb';retain(path,1);g=Gltf(path);a=next(a for a in g.g['animations'] if a['name']=='Idle_Loop')
    channel=next(c for c in a['channels'] if c['target']['path']=='rotation' and g.g['nodes'][c['target']['node']]['name']=='head') if any(g.g['nodes'][c['target']['node']]['name']=='head' for c in a['channels']) else next(c for c in a['channels'] if c['target']['path']=='rotation' and g.g['nodes'][c['target']['node']]['name'].lower()=='head')
    sampler=a['samplers'][channel['sampler']];times=g.acc(sampler['input']).flatten();rot=g.acc(sampler['output'])
    write(OUT/'IdleHead.json',{'clip':'Idle_Loop','joint':g.g['nodes'][channel['target']['node']]['name'],'duration':float(times[-1]),'times':times.tolist(),'rotations':[dict(zip(['x','y','z','w'],map(float,q))) for q in rot]})
    generated=[{'path':str(p.relative_to(ROOT)),'sha256':sha(p.read_bytes())} for p in sorted(OUT.glob('*.json'))]
    write(DEST/'provenance.json',{'packs':packs,'sources':sources,'adaptations':generated,'method':'tools/adapt-athlete-assets.py; head/neck crop, UV color sampling, normalized face width, transformed hair, Idle_Loop head rotation only. Original hand/foot/contact rig and authored sportswear retained.'})

if __name__=='__main__':main()
