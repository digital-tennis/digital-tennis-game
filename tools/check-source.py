#!/usr/bin/env python3
"""License-free source/data checks; never substitutes for Unity runtime tests."""
import csv,json,math,pathlib,re,subprocess
root=pathlib.Path(__file__).resolve().parents[1]
def require(value,message):
    if not value:raise SystemExit(message)
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'),parse_constant=lambda value:(_ for _ in ()).throw(ValueError(value)))
guids={};sources=0
for path in (root/'Assets').rglob('*'):
    if path.suffix=='.meta':
        match=re.search(r'^guid: ([0-9a-f]{32})$',path.read_text(),re.M);require(match is not None,'Invalid meta '+str(path));guid=match[1];require(guid not in guids,'Duplicate GUID '+str(path)+' / '+str(guids.get(guid)));guids[guid]=path
    elif path.suffix in ('.cs','.asmdef','.shader','.json'):
        require(path.with_name(path.name+'.meta').is_file(),'Missing meta '+str(path));sources+=1
        if path.suffix in ('.json','.asmdef'):load(path)
manifest=load(root/'Packages/manifest.json')
for package in ('ai','protocol','controller'):
    require(re.fullmatch(r'https://github.com/digital-tennis/digital-tennis-[a-z-]+\.git#[0-9a-f]{40}',manifest['dependencies']['com.digital-tennis.'+package]) is not None,'Pin full reviewed SHA: '+package)
csv_count=0
for path in (root/'Assets/StreamingAssets').rglob('*.csv'):
    with path.open(encoding='utf-8-sig',newline='') as stream:rows=list(csv.DictReader(stream))
    require(len(rows)>0,'Empty CSV '+str(path));keys=set()
    for row in rows:
        require(None not in row and all(value is not None for value in row.values()),'Invalid CSV columns '+str(path));key=next(iter(row.values()));key=(key,row.get('language'),row.get('variant')) if path.name=='subtitles.csv' else key;require(key not in keys,'Duplicate CSV key '+str(key));keys.add(key)
        for value in row.values():
            try:number=float(value)
            except ValueError:continue
            require(math.isfinite(number),'Nonfinite CSV '+str(path))
    if path.name=='characters.csv':
        require(keys==set('KOR JPN CHN USA RUS IND DEU GBR ESP FRA AUS NLD BRA MAR ZAF'.split()),'15 original country IDs required')
        for row in rows:require(sum(float(row[key]) for key in ('serve','smash','volley','defense','stamina','speed'))==420,'Bundled initial rating budget '+row['id'])
    if path.name=='audio.csv':
        for row in rows:
            audio=(path.parent/row['file']).resolve();require(audio.is_relative_to(path.parent.resolve()) and audio.is_file(),'Missing/escaping audio '+row['file'])
    csv_count+=1
for path in ('Library/check','Temp/check','Builds/check','UserSettings/check','.work/check','bin/check','obj/check','.env'):
    require(subprocess.run(['git','check-ignore','--no-index','-q',path],cwd=root).returncode==0,'Generated/private path must be ignored: '+path)
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
for path in tracked:
    if path:require(path.split('/')[0] not in ('Library','Temp','Builds','UserSettings','.work','bin','obj') and path!='.env','Unwanted tracked file '+path)
print(json.dumps({'passed':True,'sourceFiles':sources,'uniqueGuids':len(guids),'csvFiles':csv_count,'scope':'Static source and bundled data; Unity runtime verification is separate'}))
