using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

// Deliberately C# 4 / classic mscorlib compatible. Run on the target Mono.
// A host pass establishes only that the probe works, not iPod compatibility.
public static class RuntimeProbe
{
    private static int result;

    public static int Main()
    {
        Console.WriteLine("Runtime: " + Environment.Version);
        Console.WriteLine("OS: " + Environment.OSVersion);
        Console.WriteLine("Pointer bytes: " + IntPtr.Size);
        var values = new List<int>();
        for (int i = 0; i < 1000; i++) values.Add(i);
        GC.Collect();
        if (values[999] != 999) return 1;
        var thread = new Thread(delegate() { Interlocked.Exchange(ref result, 42); });
        thread.Start();
        if (!thread.Join(10000) || result != 42) return 2;
        MethodInfo method = typeof(RuntimeProbe).GetMethod("Identity");
        if ((int)method.Invoke(null, new object[] { 17 }) != 17) return 3;
        Console.WriteLine("PASS: managed execution, generics, GC, threads, reflection");
        return 0;
    }

    public static int Identity(int value) { return value; }
}
