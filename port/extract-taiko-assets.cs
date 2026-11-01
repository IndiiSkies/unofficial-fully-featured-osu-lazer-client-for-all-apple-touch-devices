using System;using System.IO;using System.Reflection;
class ExtractTaikoAssets {
 static void Main(string[] args){Directory.CreateDirectory(args[1]);var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));
  foreach(string name in assembly.GetManifestResourceNames()){
   if(name.IndexOf("pippidon",StringComparison.OrdinalIgnoreCase)<0)continue;
   Console.WriteLine(name);
   if(!name.EndsWith(".png"))continue;
   int start=name.IndexOf("pippidon",StringComparison.OrdinalIgnoreCase);
   using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(Path.Combine(args[1],name.Substring(start))))input.CopyTo(output);
  }
 }
}
