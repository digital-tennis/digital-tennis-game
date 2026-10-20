"""Losslessly package new raw self-play evidence; preserve baseline/training artifacts."""
import gzip
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'docs/evidence/ai-full-match'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    for name in ('self-play','held-out'):
        report=FOLDER/(name+'.json');r=json.loads(report.read_text());assert r['passed']
        raw=FOLDER/(name+'-raw.jsonl');assert sha(raw)==r['rawSha256']
        packed=raw.with_suffix('.jsonl.gz')
        with packed.open('wb') as output:
            with gzip.GzipFile(filename='',mode='wb',fileobj=output,mtime=0) as gz:gz.write(raw.read_bytes())
        assert gzip.decompress(packed.read_bytes())==raw.read_bytes()
        r['uncompressedRawSha256']=r['rawSha256'];r['rawSha256']=sha(packed);r['rawFile']=packed.name
        r['sourceSha256']['tools/compact-ai-self-play.py']=sha(Path(__file__).resolve())
        if name=='held-out':
            r['limitations']=[v.replace('scripted uses away-from-opponent deep placement','scripted uses opponent-X-sign width and deep placement') for v in r['limitations']]
        if name=='self-play':
            r['trainingCompletedMatches']=sum(json.loads(line)['state']['done'] and json.loads(line)['step']<=r['steps'] for line in raw.read_text().splitlines())
            assert r['trainingCompletedMatches']>=2
        report.write_text(json.dumps(r,indent=2)+'\n');raw.unlink()
        print(packed.name,packed.stat().st_size,'bytes, lossless SHA256 checked')
if __name__=='__main__':main()
