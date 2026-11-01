using System;using System.IO;using System.Reflection;
class ExtractResultsSounds {
 static void Main(string[] args){Directory.CreateDirectory(args[1]);var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));
  foreach(string name in assembly.GetManifestResourceNames()){
   const string prefix="osu.Game.Resources.Samples.Results.";if(!name.StartsWith(prefix))continue;
   string file=name.Substring(prefix.Length);if(!(file.StartsWith("applause-") || file.StartsWith("score-panel-top-appear") || file.StartsWith("rank-impact")))continue;
   string target=Path.Combine(args[1],"results-"+file);using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(target))input.CopyTo(output);Console.WriteLine(target);
  }
 }
}
