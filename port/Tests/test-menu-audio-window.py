#!/usr/bin/env python3
import math,struct,subprocess,tempfile,wave
from pathlib import Path
root=Path(__file__).resolve().parents[2]
with tempfile.TemporaryDirectory(prefix='osu-window-') as temp:
 p=Path(temp);wav=p/'tones.wav'
 with wave.open(str(wav),'wb') as file:
  file.setnchannels(1);file.setsampwidth(2);file.setframerate(22050)
  data=bytearray()
  for i in range(220500):
   frequency=(8 if i<110250 else 48)*11025/512
   data+=struct.pack('<h',round(16383*math.sin(2*math.pi*frequency*i/22050)))
  file.writeframes(data)
 inputs=[wav]
 for ext,codec in [('mp3','libmp3lame'),('m4a','aac')]:
  target=p/('tones.'+ext);subprocess.run(['/opt/homebrew/bin/ffmpeg','-v','error','-y','-i',str(wav),'-c:a',codec,str(target)],check=True);inputs.append(target)
 exe=p/'window-tests'
 subprocess.run(['/usr/bin/clang','-O2','-Wno-deprecated-declarations',str(root/'port/Tests/MenuAudioWindowTests.m'),str(root/'port/Native/MenuAudioPCM.m'),str(root/'port/Native/MenuSpectrum.c'),'-framework','Foundation','-framework','AudioToolbox','-framework','Accelerate','-o',str(exe)],check=True)
 subprocess.run([str(exe)]+list(map(str,inputs)),check=True)
