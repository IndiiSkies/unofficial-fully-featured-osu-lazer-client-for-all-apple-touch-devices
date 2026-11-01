using System;using System.Globalization;using OsuLegacy;
class CatchConversionDump {
 static void Main(string[] args){foreach(string path in args){
  Beatmap map=Beatmap.Load(path);map.Mode=2;if(System.IO.File.Exists(path+".mods"))map.ApplyCatchMods(System.IO.File.ReadAllText(path+".mods"));var session=new Session(map);
  Console.WriteLine("MAP\t"+System.IO.Path.GetFileName(path));
  foreach(CatchObject obj in session.Catch.Objects)Console.WriteLine(obj.Index+"\t"+obj.Time.ToString("R",CultureInfo.InvariantCulture)+"\t"+obj.X.ToString("R",CultureInfo.InvariantCulture)+"\t"+(obj.HyperTarget>=0?"1":"0")+"\t"+obj.Kind);
 }}
}
