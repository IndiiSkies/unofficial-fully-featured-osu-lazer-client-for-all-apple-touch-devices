"""Compare old-runtime compatibility with .NET's LINQ/math running the same upstream evaluators."""
from pathlib import Path
import re,subprocess,json,math
root=Path(__file__).resolve().parents[2];out=root/'build/difficulty';dotnet=root/'toolchain/dotnet-10';csc=[str(dotnet/'dotnet'),str(dotnet/'sdk/10.0.100/Roslyn/bincore/csc.dll')]
compat=(root/'port/Managed/DifficultyCompat.cs').read_text();compat='using System;using System.Collections.Generic;using System.Linq;\n'+compat[compat.index('namespace OsuLegacy.Difficulty {'):];compat=re.sub(r'^namespace System.*$', '',compat,flags=re.M);compat+='\nnamespace OsuLegacy.Difficulty { public static class ReferenceList {public static void AddInPlace<T>(this List<T> list,T value) where T:IComparable<T>{int i=list.BinarySearch(value);list.Insert(i<0?~i:i,value);}}}\n';p=out/'ReferenceCompat.cs';p.write_text(compat)
refs=list((dotnet/'packs/Microsoft.NETCore.App.Ref/10.0.0/ref/net10.0').glob('*.dll'))
if not refs:raise RuntimeError('Missing .NET reference pack')
# The manifest prevents old generated files from an earlier enumeration being compiled twice.
manifest=json.loads((out/'sources.json').read_text());sources=[str(p),str(root/'port/Managed/DifficultyEngine.cs'),str(root/'port/Tests/DifficultyReference.cs')]+manifest
subprocess.run(csc+['-nologo','-noconfig','-nostdlib+','-target:exe','-optimize+','-nowarn:0105,8632','-out:'+str(out/'Reference.dll')]+['-r:'+str(r) for r in refs]+sources,check=True)
(out/'Reference.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net10.0','framework':{'name':'Microsoft.NETCore.App','version':'10.0.0'}}}))
subprocess.run(['mcs','-nostdlib','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-r:'+str(root/'build/port/OsuLegacy.app/managed/Difficulty.dll'),'-out:'+str(out/'Legacy.exe'),str(root/'port/Tests/DifficultyReference.cs')],check=True)
a=subprocess.check_output([str(dotnet/'dotnet'),str(out/'Reference.dll')],text=True)
import os
env=dict(os.environ,MONO_PATH=str(root/'build/port/OsuLegacy.app/managed'))
b=subprocess.check_output(['mono',str(out/'Legacy.exe')],env=env,text=True)
x=[float(v) for v in a.split()];y=[float(v) for v in b.split()]
assert len(x)==len(y)==20
for i,(u,v) in enumerate(zip(x,y)):
 assert math.isfinite(v) and math.isclose(u,v,rel_tol=1e-10,abs_tol=1e-10),(i,u,v)
print('PASS: 20 difficulty ratings agree with modern-runtime LINQ/math reference across modes, clock rates and HD/FL')
