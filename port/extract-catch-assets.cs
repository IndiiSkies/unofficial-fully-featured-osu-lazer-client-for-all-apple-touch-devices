using System;using System.IO;using System.Reflection;
class ExtractCatchAssets {
 static void Main(string[] args){Directory.CreateDirectory(args[1]);var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));
  foreach(string name in assembly.GetManifestResourceNames()){
   if(name.Contains("metronomelow") || name.Contains("catch-banana")){
    Console.WriteLine(name);using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(Path.Combine(args[1],name.Substring(name.LastIndexOf("metronomelow")>=0?name.LastIndexOf("metronomelow"):name.LastIndexOf("catch-banana")))))input.CopyTo(output);continue;
   }
   if(name.Contains(".Samples.Gameplay.") && (name.Contains("metronomelow") || name.Contains("resume-countdown"))){
    string sample=name.Substring(name.LastIndexOf(".Gameplay.")+10);
    Console.WriteLine(sample);using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(Path.Combine(args[1],sample)))input.CopyTo(output);continue;
   }
   if(name.IndexOf(".Gameplay.catch.",StringComparison.OrdinalIgnoreCase)<0)continue;
   Console.WriteLine(name);if(!name.EndsWith(".png"))continue;
   string file=name.Substring(name.IndexOf(".catch.")+7);
   using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(Path.Combine(args[1],file)))input.CopyTo(output);
  }
 }
}
