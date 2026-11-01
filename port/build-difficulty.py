"""Compile lazer difficulty evaluators with a corlib-only compatibility layer."""
from pathlib import Path
import re,subprocess,json
root=Path(__file__).resolve().parent.parent
out=root/'build/difficulty';out.mkdir(parents=True,exist_ok=True)
files=[(root/'full-client/osu/osu.Game.Rulesets.Mania/MathUtils/LegacySortHelper.cs','OsuLegacy.Difficulty')]
base=root/'full-client/osu/osu.Game/Rulesets/Difficulty'
for p in list((base/'Skills').glob('*.cs'))+list((base/'Utils').glob('DiffUtils*.cs'))+[base/'Preprocessing/DifficultyHitObject.cs']:files.append((p,'OsuLegacy.Difficulty'))
for mode in ['Osu','Taiko','Catch','Mania']:
 base=root/f'full-client/osu/osu.Game.Rulesets.{mode}/Difficulty'
 for folder in ['Skills','Evaluators','Preprocessing','Utils']:
  for p in (base/folder).rglob('*.cs'):
   if p.name!='LegacyScoreUtils.cs':files.append((p,'OsuLegacy.Difficulty.'+mode))
generated=[]
for num,(p,namespace) in enumerate(files):
 s=p.read_text(encoding='utf-8-sig')
 s=re.sub(r'^using (?!System(?:;|\.Collections\.Generic;|\.Linq;|\.Runtime\.CompilerServices;)).*;$','',s,flags=re.M)
 s=re.sub(r'namespace [\w.]+','namespace '+namespace,s,count=1)
 s=re.sub(r'ArgumentNullException.ThrowIfNull\((\w+)\);',r'if(\1==null)throw new ArgumentNullException("\1");',s)
 s=s.replace('IReadOnlyList<','IList<').replace('IReadOnlyCollection<','ICollection<').replace('MethodImplOptions.AggressiveInlining','(MethodImplOptions)256')
 s=s.replace('Math.Clamp(', 'Compat.Clamp(').replace('Math.Cbrt(', 'Compat.Cbrt(').replace('MathF.', 'CompatF.')
 s=re.sub(r'([\w.]+)\[\^(\d+)\]',r'\1[\1.Count-\2]',s)
 s=s.replace('double.DegreesToRadians(', 'Compat.DegreesToRadians(').replace('double.IsNormal(', 'Compat.IsNormal(').replace('double.Lerp(', 'Interpolation.Lerp(')
 s=s.replace('.OrderDescending()', '.OrderByDescending(x => x)').replace('.Order()', '.OrderBy(x => x)')
 s='using OsuLegacy.Difficulty;\nusing System.Linq;\n'+s
 target=out/(str(num)+'-'+p.name);target.write_text(s);generated.append(str(target))
# Retain upstream stacking and taiko star aggregation, omitting framework-only attributes/pp output.
p=root/'full-client/osu/osu.Game.Rulesets.Osu/Beatmaps/OsuBeatmapProcessor.cs'
s=p.read_text(encoding='utf-8-sig');s=s[s.index('        internal static void ApplyStacking'):];s=re.sub(r'\s*ArgumentOutOfRangeException.ThrowIf[^;]+;', '', s)
s='using System;using System.Collections.Generic;using System.Linq;using OsuLegacy.Difficulty;namespace OsuLegacy.Difficulty.Osu {public static class Stacking {public const int STACK_DISTANCE=3;'+s
p=out/'Stacking.cs';p.write_text(s);generated.append(str(p))
p=root/'full-client/osu/osu.Game.Rulesets.Taiko/Difficulty/TaikoDifficultyCalculator.cs'
s=p.read_text(encoding='utf-8-sig');s=s[s.index('        private double combinedDifficultyValue'):];s=s.replace('IReadOnlyList<','IList<').replace('.OrderDescending()', '.OrderByDescending(x => x)')
s='using System;using System.Collections.Generic;using System.Linq;using OsuLegacy.Difficulty;namespace OsuLegacy.Difficulty.Taiko {public class Aggregation {private const double difficulty_multiplier=.084375,rhythm_skill_multiplier=.770*difficulty_multiplier,reading_skill_multiplier=.100*difficulty_multiplier,colour_skill_multiplier=.375*difficulty_multiplier,stamina_skill_multiplier=.445*difficulty_multiplier;private double patternMultiplier,strainLengthBonus;private bool isConvert,isRelax;public double Calculate(Rhythm rhythm,Reading reading,Colour colour,Stamina stamina,bool convert,bool relax){isConvert=convert;isRelax=relax;double sv=stamina.DifficultyValue(),s=sv*stamina_skill_multiplier,c=colour.DifficultyValue()*colour_skill_multiplier;patternMultiplier=DiffUtils.Pow(s*c,.10);strainLengthBonus=1+.15*DiffUtils.ReverseLerp(stamina.CountTopWeightedStrains(sv),1000,1555);double consistency;return rescale(combinedDifficultyValue(rhythm,reading,colour,stamina,out consistency)*1.4);}'+s
p=out/'Aggregation.cs';p.write_text(s);generated.append(str(p))
(out/'sources.json').write_text(json.dumps(generated))
dotnet=root/'toolchain/dotnet-10'
subprocess.run([str(dotnet/'dotnet'),str(dotnet/'sdk/10.0.100/Roslyn/bincore/csc.dll'),'-nologo','-noconfig','-nostdlib+','-target:library','-optimize+','-nullable:disable','-nowarn:0105,8632','-r:'+str(root/'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),'-out:'+str(root/'build/port/OsuLegacy.app/managed/Difficulty.dll'),str(root/'port/Managed/DifficultyCompat.cs'),str(root/'port/Managed/DifficultyEngine.cs'),*generated],check=True)
