using System;using System.Globalization;using OsuLegacy;
class TaikoConversionDump {
 static void Main(string[] paths){System.Threading.Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
 foreach(string path in paths){var map=Beatmap.Load(path);map.ConvertToTaiko();Console.WriteLine("MAP\t"+path);
 foreach(Circle o in map.Objects)Console.WriteLine("{0:R}\t{1:R}\t{2}\t{3}\t{4}\t{5}\t{6}",o.Time,o.EndTime,o.Kind==ObjectKind.Circle && (o.HitSound&10)!=0,o.Kind==ObjectKind.Circle && (o.HitSound&10)==0,o.Kind==ObjectKind.Slider,o.Kind==ObjectKind.Spinner,o.Kind!=ObjectKind.Spinner && (o.HitSound&4)!=0);
 }}
}
