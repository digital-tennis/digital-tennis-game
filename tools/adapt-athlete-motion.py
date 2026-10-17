#!/usr/bin/env python3
"""Sample retained CC0 UAL full skeleton; retarget its frames to the contact rig.
No root motion is exported. Source bone +Y becomes target limb -Y; parent-space
rotations retain source axial twist instead of copying incompatible local axes.
"""
import importlib.util, json, hashlib
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('asset',ROOT/'tools/adapt-athlete-assets.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def quat(r):
    # Eigenvector form remains stable near the 180-degree leg bind rotations.
    k=np.array([[r[0,0]-r[1,1]-r[2,2],r[1,0]+r[0,1],r[2,0]+r[0,2],r[2,1]-r[1,2]],
                [r[1,0]+r[0,1],r[1,1]-r[0,0]-r[2,2],r[2,1]+r[1,2],r[0,2]-r[2,0]],
                [r[2,0]+r[0,2],r[2,1]+r[1,2],r[2,2]-r[0,0]-r[1,1],r[1,0]-r[0,1]],
                [r[2,1]-r[1,2],r[0,2]-r[2,0],r[1,0]-r[0,1],r.trace()]])/3
    _,v=np.linalg.eigh(k);q=v[:,-1];return q if q[3]>=0 else -q

def interpolate(times,values,t,rotation=False):
    i=max(0,min(len(times)-2,int(np.searchsorted(times,t)-1)));a=values[i];b=values[i+1];u=np.clip((t-times[i])/max(1e-9,times[i+1]-times[i]),0,1)
    if rotation:
        if np.dot(a,b)<0:b=-b
        d=np.clip(np.dot(a,b),-1,1)
        if d<.9995:
            theta=np.arccos(d);return (a*np.sin((1-u)*theta)+b*np.sin(u*theta))/np.sin(theta)
        q=a*(1-u)+b*u;return q/np.linalg.norm(q)
    return a*(1-u)+b*u

def main():
    cfgpath=ROOT/'Assets/Resources/Characters/motion-retarget.json';cfg=json.loads(cfgpath.read_text());source=ROOT/cfg['source'];g=m.Gltf(source);nodes=g.g['nodes'];names={n['name']:i for i,n in enumerate(nodes)}
    clips=[]
    for name in cfg['clips']:
        anim=next(a for a in g.g['animations'] if a['name']==name);channels=[]
        for c in anim['channels']:
            s=anim['samplers'][c['sampler']];assert s.get('interpolation','LINEAR')=='LINEAR'
            channels.append((c['target']['node'],c['target']['path'],g.acc(s['input']).flatten(),g.acc(s['output'])))
        duration=max(x[2][-1] for x in channels);frames=[]
        for t in np.linspace(0,duration,round(duration*cfg['sampleRate'])+1):
            pose=[dict(n) for n in nodes]
            for i,path,times,values in channels:pose[i][path]=interpolate(times,values,t,path=='rotation').tolist()
            worlds={}
            def visit(i,parent):
                worlds[i]=parent@g.matrix(pose[i])
                for child in nodes[i].get('children',[]):visit(child,worlds[i])
            for i in g.g['scenes'][g.g.get('scene',0)]['nodes']:visit(i,np.eye(4))
            target={};rotations=[]
            for bone in cfg['bones']:
                r=worlds[names[bone['source']]][:3,:3];r=r/np.linalg.norm(r,axis=0)
                r=r@np.diag(bone['frameFlip'])
                target[bone['path']]=r
                parent=target.get(bone['parent'],np.eye(3));q=quat(parent.T@r)
                rotations.append(dict(zip('xyzw',map(float,q))))
            feet=[]
            pelvis=worlds[names['pelvis']][:3,3]
            for side in ['l','r']:
                ankle=worlds[names['foot_'+side]][:3,3]
                feet.append(dict(x=float(ankle[0]-pelvis[0]),y=float(ankle[1]),z=float(ankle[2]-pelvis[2])))
            frames.append(dict(rotations=rotations,feet=feet,pelvisY=float(pelvis[1])))
        # Source loop endpoints differ slightly. Close the final authored window with
        # shortest-path quaternion interpolation, rather than snap at wraparound.
        window=max(2,round(cfg['loopBlendSeconds']*cfg['sampleRate']))
        for offset in range(window):
            weight=(offset+1)/window;weight=weight*weight*(3-2*weight)
            f=frames[len(frames)-window+offset]
            for i,q in enumerate(f['rotations']):
                a=np.array([q[k] for k in 'xyzw']);b=np.array([frames[0]['rotations'][i][k] for k in 'xyzw'])
                if np.dot(a,b)<0:b=-b
                result=a*(1-weight)+b*weight;result/=np.linalg.norm(result)
                f['rotations'][i]=dict(zip('xyzw',map(float,result)))
            f['pelvisY']=f['pelvisY']*(1-weight)+frames[0]['pelvisY']*weight
            for i in range(2):
                for axis in 'xyz':f['feet'][i][axis]=f['feet'][i][axis]*(1-weight)+frames[0]['feet'][i][axis]*weight
        clips.append(dict(name=name,duration=float(duration),frames=frames))
    out=ROOT/'Assets/Resources/Characters/Acquired/FullBodyMotion.json';out.write_text(json.dumps(dict(clips=clips),separators=(',',':'))+'\n')
    paths=[source,cfgpath,ROOT/'Assets/ThirdParty/Quaternius/1/License.txt',out]
    manifest=dict(license='CC0-1.0',provenance='provenance.json',method='tools/adapt-athlete-motion.py',runtime='AcquiredAthleteMotion; KOR/JPN only; no root motion',files=[dict(path=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths],clips=[dict(name=c['name'],frames=len(c['frames']),bones=len(cfg['bones'])) for c in clips])
    (ROOT/'Assets/ThirdParty/Quaternius/motion-bindings.json').write_text(json.dumps(manifest,indent=2)+'\n');print('Retargeted full-body '+', '.join(cfg['clips']))
if __name__=='__main__':main()
