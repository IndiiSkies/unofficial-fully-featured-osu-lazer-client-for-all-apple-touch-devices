using System;using System.IO;using System.Reflection;
class ExtractNotificationIcon {
    static void Main(string[] args){
        var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));
        foreach(string name in assembly.GetManifestResourceNames())if(name.EndsWith(".Icons.notification.png",StringComparison.OrdinalIgnoreCase)){
            using(var input=assembly.GetManifestResourceStream(name))using(var output=File.Create(args[1]))input.CopyTo(output);
            Console.WriteLine(name);return;
        }
        throw new Exception("Upstream notification icon not found");
    }
}
