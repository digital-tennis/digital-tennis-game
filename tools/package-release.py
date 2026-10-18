#!/usr/bin/env python3
"""Package existing desktop builds locally, with source revision and checksums. Does not publish."""
import argparse,hashlib,json,subprocess,zipfile,tempfile,shutil,os
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--platform',choices=['mac','windows'],required=True);p.add_argument('--configuration',choices=['release','development'],default='release');p.add_argument('--out',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];source=root/'Builds'/('release' if a.configuration=='release' else '')/a.platform
expected=source/('DigitalTennis.app' if a.platform=='mac' else 'DigitalTennis.exe')
if not expected.exists():raise SystemExit('Build first: bash tools/unity.sh '+('release-' if a.configuration=='release' else 'build-')+a.platform)
a.out.mkdir(parents=True,exist_ok=True);a.out=a.out.resolve()
sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip();dirty=bool(subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).strip())
build=json.loads((source/'build-info.json').read_text());
if build['sourceCommit']!=sha:raise SystemExit('Build source revision differs from HEAD; rebuild first')
if a.configuration=='release' and (dirty or build.get('dirtyWorkingTree',True) or build['development'] or build['allowDebugging']):raise SystemExit('Release packaging requires clean committed source and no Development/AllowDebugging flags')
native=source/('DigitalTennis.app/Contents/Resources/Data/StreamingAssets/ControllerNative' if a.platform=='mac' else 'DigitalTennis_Data/StreamingAssets/ControllerNative')
helper=native/('digital-tennis-ble' if a.platform=='mac' else 'digital-tennis-ble.exe')
for required in [helper,native/'native-build.json',native/'Licenses/Unity-WebRTC/LICENSE.md',native/'Licenses/Unity-WebRTC/Third Party Notices.md',native/'Licenses/DigitalTennis.Game/LICENSE',native/'Licenses/DigitalTennis.Game/NOTICE',*[native/'Licenses'/package/'LICENSE' for package in ('com.digital-tennis.protocol','com.digital-tennis.controller','com.digital-tennis.ai')]]:
 if not required.is_file() or required.stat().st_size==0:raise SystemExit('Missing native runtime/provenance/license: '+str(required))
if a.platform=='windows' and not any(p.is_file() and 'Microsoft' in str(p) for p in (native/'Licenses').rglob('*')):raise SystemExit('Missing redistributed .NET runtime notices')
native_manifest={'helperSha256':hashlib.file_digest(helper.open('rb'),'sha256').hexdigest(),'build':json.loads((native/'native-build.json').read_text()),'notices':[str(p.relative_to(native)) for p in sorted((native/'Licenses').rglob('*')) if p.is_file()]}
archive=a.out/('DigitalTennis-'+a.configuration+'-'+a.platform+'-'+sha[:8]+'.zip')
if archive.exists():raise SystemExit('Refusing to overwrite existing archive '+str(archive))
if a.platform=='mac':
 with tempfile.TemporaryDirectory(prefix='digital-tennis-release-') as tmp:
  staged=Path(tmp)/expected.name;shutil.copytree(expected,staged,symlinks=True)
  # Strip only Finder artwork/resource metadata from this temporary copy. Never remove quarantine/security attributes.
  for attr in ['com.apple.FinderInfo','com.apple.ResourceFork']:
   subprocess.run(['xattr','-dr',attr,str(staged)],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=False)
  subprocess.run(['codesign','--verify','--deep','--strict',str(staged)],check=True)
  subprocess.run(['ditto','-c','-k','--sequesterRsrc','--keepParent',str(staged),str(archive)],check=True)
else:
 with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
  for path in sorted(source.rglob('*')):
   if path.is_file() and path.relative_to(source).parts[0] in ['DigitalTennis.exe','DigitalTennis_Data','MonoBleedingEdge','D3D12','UnityPlayer.dll','UnityCrashHandler64.exe','baselib.dll','WinPixEventRuntime.dll'] and path.suffix.lower() not in ['.pdb','.mdb']:z.write(path,path.relative_to(source))
checksum=hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()
manifest={'platform':a.platform,'configuration':a.configuration,'development':build['development'],'allowDebugging':build['allowDebugging'],'sourceCommit':sha,'dirtyWorkingTree':dirty,'sha256':checksum,'archive':archive.name,'unity':'6000.3.26f1','runtimeVerified':'Mac M2 Max GPU batch; see docs/polish-validation.md' if a.platform=='mac' else 'Cross-build only; physical Windows execution still required','distribution':'Local unsigned/ad-hoc preview;  no Developer ID notarization or paid distribution performed','macSignatureVerified':a.platform=='mac'}
manifest['controllerNative']=native_manifest
(archive.with_suffix('.json')).write_text(json.dumps(manifest,indent=2)+'\n');(archive.with_suffix('.sha256')).write_text(checksum+'  '+archive.name+'\n')
print(json.dumps(manifest,indent=2))
