"""Produce portable Torch parity cases for the exact resumed inference asset."""
import hashlib,json,sys
from pathlib import Path
import numpy as np
import torch
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'.work/ai-full-match/Training'))
from full_match import normalize,SCALES
p=ROOT/'Assets/Resources/FullMatchAI/resumed-ppo.json';d=json.loads(p.read_text())
assert p.read_bytes()==(ROOT/'docs/evidence/ai-full-match/resume-ppo.json').read_bytes()
rng=np.random.default_rng(91027);cases=[]
for _ in range(64):
 raw=(rng.uniform(-1,1,32)*SCALES).astype(np.float32);obs=normalize(raw);x=torch.tensor(obs)
 for i,n in [(1,32),(2,32),(3,6)]:
  x=torch.nn.functional.linear(x,torch.tensor(d['w'+str(i)]).reshape(n,-1),torch.tensor(d['b'+str(i)]))
  if i<3:x=torch.tanh(x)
 cases.append(dict(raw=raw.tolist(),observation=obs.tolist(),logits=x.tolist(),action=int(x.argmax())))
out=ROOT/'Assets/Resources/FullMatchAI/resumed-parity.json'
out.write_text(json.dumps(dict(modelSha256=hashlib.sha256(p.read_bytes()).hexdigest(),cases=cases),indent=2)+'\n')
print('PASS resumed model export and 64 Torch parity cases')
