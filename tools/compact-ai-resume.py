"""Keep the verified compressed receipt; archive large local trajectories outside Git."""
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import uuid
ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'docs/evidence/ai-full-match'
r=json.loads((folder/'resume.json').read_text());raw=folder/r['rawFile']
assert r['passed'] and hashlib.sha256(raw.read_bytes()).hexdigest()==r['rawSha256']
combined=gzip.decompress(raw.read_bytes())
a=folder/'resume-interrupt-raw.jsonl';b=folder/'resume-continuation-raw.jsonl'
if a.exists() or b.exists():assert a.read_bytes()+b.read_bytes()==combined
archive=ROOT/'.work/ai-resume-raw'/uuid.uuid4().hex
archive.mkdir(parents=True)
for name in ('resume-interrupt-raw.jsonl','resume-continuation-raw.jsonl','resume-raw.jsonl'):
    path=folder/name
    if path.exists():shutil.move(str(path),str(archive/name))
print('Verified compressed resume evidence; uncompressed local logs archived under ignored .work')
