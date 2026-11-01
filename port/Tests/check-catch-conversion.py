"""Compare the port against lazer's committed conversion expectations."""
from pathlib import Path
import json, os, re, subprocess
root=Path(__file__).resolve().parents[2]
fixtures=root/'full-client/osu/osu.Game.Rulesets.Catch.Tests/Resources/Testing/Beatmaps'
out=root/'build/catch-parity';out.mkdir(parents=True,exist_ok=True)
(out/'fixture.wav').write_bytes(b'fixture')
test=(root/'full-client/osu/osu.Game.Rulesets.Catch.Tests/CatchBeatmapConversionTest.cs').read_text(encoding='utf-8-sig')
cases=re.findall(r'\[TestCase\("([^\"]+)"([^\n]*)\)\]',test)
names=[name for name,_ in cases]
paths=[]
for name,mods in cases:
    p=out/(name+'.osu')
    text=(fixtures/(name+'.osu')).read_text(encoding='utf-8-sig')
    text=re.sub(r'^AudioFilename:.*$', '',text,flags=re.M)
    if '[General]' not in text:text=text.replace('\n','\n[General]\n',1)
    text=text.replace('[General]','[General]\nAudioFilename: fixture.wav')
    p.write_text(text);paths.append(p)
    Path(str(p)+'.mods').write_text(','.join(flag for key,flag in [('CatchModHardRock','HR'),('CatchModEasy','EZ')] if key in mods))
core=root/'build/port/OsuLegacy.app/managed/OsuLegacy.Core.dll'
exe=out/'CatchConversionDump.exe'
subprocess.run(['mcs','-nostdlib','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-r:'+str(core),'-out:'+str(exe),str(root/'port/Tests/CatchConversionDump.cs')],check=True)
result=subprocess.run(['mono',str(exe),*map(str,paths)],capture_output=True,text=True,env=dict(os.environ,MONO_PATH=str(core.parent)))
if result.returncode:raise RuntimeError(result.stderr)
actual={};name=None
for line in result.stdout.splitlines():
    f=line.split('\t')
    if f[0]=='MAP':name=Path(f[1]).stem;actual[name]={}
    else:actual[name].setdefault(int(f[0]),[]).append((float(f[1]),float(f[2]),f[3]=='1'))
checks=0;errors=[]
for name in names:
    mappings=json.loads((fixtures/(name+'-expected-conversion.json')).read_text())['Mappings']
    for i,mapping in enumerate(mappings):
        expected=mapping['Objects'];got=actual[name].get(i,[])
        if len(expected)!=len(got):errors.append(f'{name} object {i}: count {len(got)} != {len(expected)}');continue
        for n,(want,item) in enumerate(zip(expected,got)):
            checks+=1
            if abs(want['StartTime']-item[0])>3 or abs(want['Position']-item[1])>3 or want['HyperDash']!=item[2]:
                errors.append(f'{name} object {i}/{n}: {item} != {want}')
if errors:
    print('\n'.join(errors[:25]));raise SystemExit(f'FAIL: {len(errors)} conversion differences / {checks} checks')
print(f'PASS: {checks} nested catch objects across {len(names)} upstream conversion fixtures, including HR/EZ (official 3-unit tolerance; audio rate not tested)')
