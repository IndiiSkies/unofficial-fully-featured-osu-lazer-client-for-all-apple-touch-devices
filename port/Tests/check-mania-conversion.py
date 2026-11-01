"""Native column mapping and actual upstream pattern-generator parity."""
from pathlib import Path
import json, os, re, subprocess
root=Path(__file__).resolve().parents[2]
fixtures=root/'full-client/osu/osu.Game.Rulesets.Mania.Tests/Resources/Testing/Beatmaps'
out=root/'build/mania-parity';out.mkdir(parents=True,exist_ok=True)
(out/'fixture.wav').write_bytes(b'fixture')
test=(root/'full-client/osu/osu.Game.Rulesets.Mania.Tests/ManiaBeatmapConversionTest.cs').read_text(encoding='utf-8-sig')
names=re.findall(r'^\s*\[TestCase\("([^\"]+)"\)\]',test,re.M)
paths=[]
for name in names:
    p=out/(name+'.osu');text=(fixtures/p.name).read_text(encoding='utf-8-sig')
    text=re.sub(r'^AudioFilename:.*$', '',text,flags=re.M)
    if '[General]' not in text:text=text.replace('\n','\n[General]\n',1)
    p.write_text(text.replace('[General]','[General]\nAudioFilename: fixture.wav'));paths.append(p)
core=root/'build/port/OsuLegacy.app/managed/OsuLegacy.Core.dll';exe=out/'ManiaConversionDump.exe'
subprocess.run(['mcs','-nostdlib','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-r:'+str(core),'-out:'+str(exe),str(root/'port/Tests/ManiaConversionDump.cs')],check=True)
result=subprocess.run(['mono',str(exe),*map(str,paths)],capture_output=True,text=True,env=dict(os.environ,MONO_PATH=str(core.parent)),check=True)
actual={}
for line in result.stdout.splitlines():
    f=line.split('\t')
    if f[0]=='MAP':name=Path(f[1]).stem;actual[name]=[]
    else:actual[name].append((float(f[0]),float(f[1]),int(f[2])))
checks=0;errors=[]
for name in names:
    expected=sorted((o['StartTime'],o['EndTime'],o['Column']) for m in json.loads((fixtures/(name+'-expected-conversion.json')).read_text())['Mappings'] for o in m['Objects'])
    got=sorted(actual[name])
    if len(expected)!=len(got):errors.append(f'{name}: count {len(got)} != {len(expected)}');continue
    for i,(want,item) in enumerate(zip(expected,got)):
        checks+=1
        if abs(want[0]-item[0])>2 or abs(want[1]-item[1])>2 or want[2]!=item[2]:errors.append(f'{name}/{i}: {item} != {want}')
if errors:raise SystemExit('\n'.join(errors[:25]))
print(f'PASS: {checks} mania notes across {len(names)} upstream conversion/native 1K–18K fixtures (2ms timing tolerance; RNG snapshots not tested)')
