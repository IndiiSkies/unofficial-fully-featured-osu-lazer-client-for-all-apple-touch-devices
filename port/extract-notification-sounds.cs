using System;using System.IO;using System.Reflection;
class ExtractNotificationSounds {
    static void Main(string[] args){
        var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));Directory.CreateDirectory(args[1]);
        foreach(string name in assembly.GetManifestResourceNames())foreach(string sample in new string[]{"notification-default","notification-error"})
            if(name.EndsWith("."+sample+".wav"))using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(Path.Combine(args[1],sample+".wav"))){input.CopyTo(output);Console.WriteLine(name);}
    }
}
