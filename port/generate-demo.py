#!/usr/bin/env python3
"""Create a small original click track and circle-only smoke-test beatmap."""
from array import array
from pathlib import Path
import math
import sys
import wave

destination = Path(sys.argv[1])
destination.mkdir(parents=True, exist_ok=True)
rate = 16000
samples = array("h")
for i in range(rate * 20):
    phase = (i / rate) % 0.5
    value = math.sin(2 * math.pi * 880 * phase) * math.exp(-phase * 100) if phase < 0.08 else 0
    samples.append(int(value * 18000))
if sys.byteorder != "little":
    samples.byteswap()
with wave.open(str(destination / "demo.wav"), "wb") as output:
    output.setparams((1, 2, rate, 0, "NONE", "not compressed"))
    output.writeframes(samples.tobytes())
header = """osu file format v14

[General]
AudioFilename: demo.wav
Mode: 0

[Metadata]
Title: ARMv6 first steps
Artist: Generated click track
Creator: Legacy port
Version: Circles

[Difficulty]
CircleSize: 4
OverallDifficulty: 5
ApproachRate: 5

[Events]
0,0,"background.jpg",0,0

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
"""
points = [(128, 192), (256, 96), (384, 192), (256, 288)]
objects = ["%d,%d,%d,1,0,0:0:0:0:" % (*points[i % 4], 2000 + 500 * i) for i in range(32)]
(destination / "First steps.osu").write_text(header + "\n".join(objects) + "\n")
mixed_header = header.replace("ARMv6 first steps", "Sliders and spinners").replace("Version: Circles", "Version: Mixed objects")
mixed_header = mixed_header.replace("ApproachRate: 5", "ApproachRate: 5\nSliderMultiplier: 1.4\nSliderTickRate: 1")
mixed_objects = objects[:8] + [
    "128,192,6500,2,0,L|384:192,2,256",
    "96,192,9000,2,0,B|192:64|320:64|416:192,1,350",
    "128,192,11000,2,0,P|256:64|384:192,1,402.12386",
    "256,192,14000,8,0,18000",
]
(destination / "Sliders and spinners.osu").write_text(mixed_header + "\n".join(mixed_objects) + "\n")
taiko_header = mixed_header.replace("Mode: 0", "Mode: 1").replace("Sliders and spinners", "Taiko first steps").replace("Version: Mixed objects", "Version: Taiko basics")
taiko_objects = ["256,192,%d,1,%d,0:0:0:0:" % (2000 + i * 500, [0, 2, 0, 8, 4, 6, 0, 2][i % 8]) for i in range(16)]
taiko_objects += ["256,192,10500,2,0,L|536:192,1,280", "256,192,12500,1,0", "256,192,13000,1,2", "256,192,14000,8,0,17000", "256,192,18000,1,4"]
(destination / "Taiko first steps.osu").write_text(taiko_header + "\n".join(taiko_objects) + "\n")
catch_header = mixed_header.replace("Mode: 0", "Mode: 2").replace("Sliders and spinners", "Catch first steps").replace("Version: Mixed objects", "Version: Fruits, droplets and bananas")
catch_objects = ["%d,192,%d,1,0" % (x,2000+i*750) for i,x in enumerate([256,192,320,128,384])]
catch_objects += ["128,192,6000,2,0,B|256:96|384:192,2,280", "32,192,9000,1,0", "480,192,9200,1,0", "256,192,12000,8,0,16000", "192,192,17500,1,0", "320,192,18500,1,0"]
(destination / "Catch first steps.osu").write_text(catch_header + "\n".join(catch_objects) + "\n")
mania_header = mixed_header.replace("Mode: 0", "Mode: 3").replace("Sliders and spinners", "Mania first steps").replace("Version: Mixed objects", "Version: 4K taps, chords and holds")
mania_objects = ["%d,192,%d,1,0,0:0:0:0:" % (64+128*(i%4),2000+i*500) for i in range(12)]
mania_objects += ["64,192,8500,1,0", "448,192,8500,1,0", "192,192,9500,128,0,11500:0:0:0:0:", "320,192,10000,1,0", "448,192,10500,1,0", "64,192,12000,128,0,14000:0:0:0:0:", "448,192,12000,128,0,14000:0:0:0:0:", "192,192,15000,1,0", "320,192,15000,1,0", "64,192,16000,1,0", "192,192,16500,1,0", "320,192,17000,1,0", "448,192,17500,1,0"]
(destination / "Mania first steps.osu").write_text(mania_header + "\n".join(mania_objects) + "\n")
