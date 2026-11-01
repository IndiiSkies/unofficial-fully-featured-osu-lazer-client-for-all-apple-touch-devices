"""Compile lazer's original pattern algorithms against the iPod's corlib only.

Compatibility changes are mechanical; upstream source remains untouched.
No LINQ/System.Core runtime dependency is introduced on the old device.
"""
from pathlib import Path
import re, subprocess
root=Path(__file__).resolve().parent.parent
out=root/'build/mania-converter';out.mkdir(parents=True,exist_ok=True)
src=root/'full-client/osu/osu.Game.Rulesets.Mania/Beatmaps/Patterns'
files=[src/'Pattern.cs',src/'PatternGenerator.cs']+[src/'Legacy'/name for name in ['PatternType.cs','LegacyPatternGenerator.cs','HitCirclePatternGenerator.cs','SliderPatternGenerator.cs','SpinnerPatternGenerator.cs']]
files.append(root/'full-client/osu/osu.Game/Utils/LegacyRandom.cs')
generated=[]
for p in files:
    text=p.read_text(encoding='utf-8-sig')
    text=re.sub(r'^using (?!System(?:;|\.Collections\.Generic;|\.Linq;|\.Diagnostics;)).*;$','',text,flags=re.M)
    text=re.sub(r'namespace [\w.]+','namespace OsuLegacy.ManiaConversion',text,count=1)
    text=re.sub(r'^\s*\[MemberNotNull\(.*\)\]\s*$','',text,flags=re.M)
    text=re.sub(r'ArgumentNullException.ThrowIfNull\((\w+)\);',r'if(\1 == null)throw new ArgumentNullException("\1");',text)
    text=text.replace('[InstantHandle] ','').replace('HashSet<int>','List<int>')
    # List storage must retain HashSet uniqueness (only <=18 keys per pattern).
    text=text.replace('containedColumns.Add(hitObject.Column);','if(!containedColumns.Contains(hitObject.Column))containedColumns.Add(hitObject.Column);')
    text=text.replace('containedColumns.Add(h.Column);','if(!containedColumns.Contains(h.Column))containedColumns.Add(h.Column);')
    text='using Math = OsuLegacy.ManiaConversion.LegacyMath;\nusing MathF = OsuLegacy.ManiaConversion.LegacyMathF;\n'+text
    target=out/p.name;target.write_text(text);generated.append(str(target))
dotnet=root/'toolchain/dotnet-10'
subprocess.run([str(dotnet/'dotnet'),str(dotnet/'sdk/10.0.100/Roslyn/bincore/csc.dll'),'-nologo','-noconfig','-nostdlib+','-target:library','-optimize+','-nullable:disable','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-out:'+str(root/'build/port/OsuLegacy.app/managed/ManiaConversion.dll'),str(root/'port/Managed/ManiaConversionCompat.cs'),*generated],check=True)
