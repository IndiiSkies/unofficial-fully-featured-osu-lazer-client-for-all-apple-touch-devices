"""Compare conversion types, samples and timing against lazer's fixtures."""
from pathlib import Path
import json, os, re, subprocess
root=Path(__file__).resolve().parents[2]
fixtures=root/'full-client/osu/osu.Game.Rulesets.Taiko/Resources/Testing/Beatmaps'
out=root/'build/taiko-parity';out.mkdir(parents=True,exist_ok=True)
(out/'fixture.wav').write_bytes(b'fixture')
test=(root/'full-client/osu/osu.Game.Rulesets.Taiko.Tests/TaikoBeatmapConversionTest.cs').read_text(encoding='utf-8-sig')
names=re.findall(r'\[TestCase\("([^\"]+)"\)\]',test)
paths=[]
for name in names:
    p=out/(name+'.osu');text=(fixtures/p.name).read_text(encoding='utf-8-sig')
    text=re.sub(r'^AudioFilename:.*$', '',text,flags=re.M)
    if '[General]' not in text:text=text.replace('\n','\n[General]\n',1)
    p.write_text(text.replace('[General]','[General]\nAudioFilename: fixture.wav'));paths.append(p)
core=root/'build/port/OsuLegacy.app/managed/OsuLegacy.Core.dll';exe=out/'TaikoConversionDump.exe'
subprocess.run(['mcs','-nostdlib','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-r:'+str(core),'-out:'+str(exe),str(root/'port/Tests/TaikoConversionDump.cs')],check=True)
result=subprocess.run(['mono',str(exe),*map(str,paths)],capture_output=True,text=True,env=dict(os.environ,MONO_PATH=str(core.parent)),check=True)
actual={}
for line in result.stdout.splitlines():
    f=line.split('\t')
    if f[0]=='MAP':name=Path(f[1]).stem;actual[name]=[]
    else:actual[name].append([float(f[0]),float(f[1])]+[v=='True' for v in f[2:]])
checks=0;errors=[]
for name in names:
    expected=[o for m in json.loads((fixtures/(name+'-expected-conversion.json')).read_text())['Mappings'] for o in m['Objects']]
    got=actual[name]
    if len(expected)!=len(got):errors.append(f'{name}: count {len(got)} != {len(expected)}');continue
    for i,(want,item) in enumerate(zip(expected,got)):
        checks+=1
        if abs(want['StartTime']-item[0])>2 or abs(want['EndTime']-item[1])>2 or item[2:]!=[want[k] for k in ['IsRim','IsCentre','IsDrumRoll','IsSwell','IsStrong']]:errors.append(f'{name}/{i}: {item} != {want}')
if errors:raise SystemExit('\n'.join(errors[:20]))
print(f'PASS: {checks} taiko objects across {len(names)} upstream fixtures (official 2ms tolerance)')
