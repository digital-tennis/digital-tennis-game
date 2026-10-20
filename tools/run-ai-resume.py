"""Keep Unity suspended across two separate training processes; never score in proxy."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import socket
import subprocess
import threading
import time
import uuid
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]


def relay(listener,errors):
    upstream=None
    try:
        for phase in range(2):
            client,_=listener.accept()
            with client,client.makefile('rwb') as wire:
                if upstream is None:
                    deadline=time.monotonic()+240
                    while True:
                        try:upstream=socket.create_connection(('127.0.0.1',18572),timeout=5);break
                        except OSError:
                            if time.monotonic()>deadline:raise
                            time.sleep(.25)
                    upstream.settimeout(600);upstream.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1);unity=upstream.makefile('rwb')
                client.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
                while True:
                    line=wire.readline()
                    if not line:break
                    unity.write(line);unity.flush();reply=unity.readline()
                    if not reply:raise RuntimeError('Unity disconnected')
                    wire.write(reply);wire.flush()
                    if json.loads(line)['op']=='close':return
    except Exception as e:errors.append(str(e))
    finally:
        if upstream is not None:upstream.close()
        listener.close()


def main():
    p=argparse.ArgumentParser();p.add_argument('--python',required=True);p.add_argument('--ai',type=Path,required=True);a=p.parse_args();ai=a.ai.resolve()
    output=ROOT/'docs/evidence/ai-full-match';assert json.loads((output/'self-play.json').read_text())['passed']
    # Each explicit run has its own snapshot directory; never overwrite frozen actors.
    pool=output/'resume-pool'
    if pool.exists() and any(pool.iterdir()):
        archive=ROOT/'.work'/('prior-resume-pool-'+uuid.uuid4().hex);shutil.move(str(pool),str(archive))
    xml=ROOT/'.work/test-ai-resume.xml';xml.unlink(missing_ok=True)
    listener=socket.socket();listener.bind(('127.0.0.1',0));listener.listen(2);port=listener.getsockname()[1]
    errors=[];thread=threading.Thread(target=relay,args=(listener,errors),daemon=True);thread.start();lease=uuid.uuid4().hex
    with (ROOT/'.work/logs/resume-launch.log').open('w') as log:
        unity=subprocess.Popen(['bash','tools/unity.sh','test-ai-resume'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
        try:
            for phase in ('interrupt','continue'):
                subprocess.run([a.python,str(ai/'Training/resume.py'),'--root',str(ai),'--output',str(output),'--port',str(port),'--phase',phase,'--lease',lease],cwd=ROOT,check=True)
            thread.join(timeout=10)
            if thread.is_alive() or errors:raise RuntimeError('Bridge lease relay failed: '+str(errors))
            if unity.wait(timeout=120)!=0:raise RuntimeError('Unity resume bridge failed')
        finally:
            listener.close()
            if unity.poll() is None:unity.terminate();unity.wait(timeout=30)
    assert ET.parse(xml).getroot().get('result')=='Passed'
    shutil.copy2(xml,output/'resume-tests.xml')
    for name in ('checkpoint.py','resume.py'):shutil.copy2(ai/'Training'/name,output/name)
    path=output/'resume.json';r=json.loads(path.read_text());r['unityTestsSha256']=hashlib.sha256((output/'resume-tests.xml').read_bytes()).hexdigest();path.write_text(json.dumps(r,indent=2)+'\n')
    print('Actual Unity full-state trainer-process exit/load/8192-step continuation passed')
if __name__=='__main__':main()
