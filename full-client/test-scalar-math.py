#!/usr/bin/env python3
"""Compile the real scalar branches against the bundled .NET 4 core library."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parent.parent
source = (root / 'full-client/osu-framework/osu.Framework/Utils/PathApproximator.cs').read_text()
methods = []
for name in ('matLerp', 'matProduct', 'matScale', 'matDiff', 'matmul', 'getDistanceDistribution'):
    marker = ('private static void ' if name == 'getDistanceDistribution' else 'private static unsafe void ') + name + '('
    start = source.index(marker)
    end = source.index('\n        }', start) + len('\n        }')
    methods.append(source[start:end])

test = r'''
using System;
class ScalarMathCheck {
    // Only these two MathF calls occur in the extracted distance helper.
    static class MathF {
        public static float Pow(float x, float y) { return (float)Math.Pow(x,y); }
        public static float Sqrt(float x) { return (float)Math.Sqrt(x); }
    }
    METHODS
    static int count;
    static void Check(bool ok) { count++; if (!ok) throw new Exception("Check " + count); }
    static bool Near(float a, float b) { return Math.Abs(a-b) < 0.00001f; }
    public static void Main() {
        var a = new float[,] {{1,2,3},{4,5,6}};
        var b = new float[,] {{6,5,4},{3,2,1}};
        var r = new float[2,3];
        matLerp(a,b,0.25f,r);
        Check(Near(r[0,0],2.25f) && Near(r[1,2],4.75f));
        matLerp(a,b,0.25f,b); // Optimiser updates its second input in place.
        Check(Near(b[0,0],r[0,0]) && Near(b[1,2],r[1,2]));
        bool rejected = false;
        try { matLerp(a,b,0.5f,a); } catch (ArgumentException) { rejected=true; }
        Check(rejected);
        matProduct(a,a,r);
        Check(r[0,1] == 4 && r[1,2] == 36);
        matProduct(a,a,a);
        Check(a[0,1] == 4 && a[1,2] == 36);
        matScale(a,0.5f,a);
        Check(a[0,1] == 2 && a[1,2] == 18);
        matDiff(a,a,a);
        Check(a[0,0] == 0 && a[1,2] == 0);
        var product = new float[2,2];
        matmul(new float[,]{{1,2,3},{4,5,6}}, new float[,]{{7,8,9},{10,11,12}}, product);
        Check(product[0,0] == 50 && product[0,1] == 68 && product[1,0] == 122 && product[1,1] == 167);
        var distance = new float[3];
        getDistanceDistribution(new float[,]{{0,3,3},{0,4,9}},distance);
        Check(distance[0] == 0 && Near(distance[1],0.5f) && distance[2] == 1);
        getDistanceDistribution(new float[,]{{0,0,0},{0,0,0}},distance,1);
        Check(distance[0] == 0 && Near(distance[1],0.5f) && distance[2] == 1);
        Console.WriteLine("PASS: " + count + " scalar compatibility checks against .NET 4 references");
    }
}
'''.replace('METHODS', '\n'.join(methods))
out = root / 'build/full-client-scalar-tests'
out.mkdir(parents=True, exist_ok=True)
(out / 'ScalarMathCheck.cs').write_text(test)
subprocess.run(['mcs', '-unsafe', '-define:OSU_LEGACY_SCALAR', '-nostdlib',
                '-r:' + str(root / 'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),
                '-out:' + str(out / 'ScalarMathCheck.exe'), str(out / 'ScalarMathCheck.cs')], check=True)
subprocess.run(['mono', str(out / 'ScalarMathCheck.exe')], check=True)
