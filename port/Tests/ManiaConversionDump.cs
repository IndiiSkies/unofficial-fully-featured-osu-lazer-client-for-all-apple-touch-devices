using System;using System.Globalization;using OsuLegacy;
class ManiaConversionDump {
 static void Main(string[] paths){System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
 foreach(string path in paths){var map=Beatmap.Load(path);if(map.Mode==0)ManiaConversionAdapter.Prepare(map,true);Console.WriteLine("MAP\t"+path);
 foreach(Circle o in map.Objects)Console.WriteLine("{0:R}\t{1:R}\t{2}",o.Time,o.EndTime,o.Column);
 }}
}
