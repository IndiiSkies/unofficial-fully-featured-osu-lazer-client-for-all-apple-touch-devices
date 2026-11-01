import math,struct,wave,tempfile,subprocess,pathlib
with tempfile.TemporaryDirectory(prefix='osu-rate-') as temp:
    temp=pathlib.Path(temp);source=temp/'tone.wav'
    with wave.open(str(source),'wb') as out:
        out.setparams((2,2,44100,0,'NONE','not compressed'))
        out.writeframes(b''.join(struct.pack('<hh',*(2*[int(16000*math.sin(i*2*math.pi*440/44100))])) for i in range(44100*3)))
    for rate,pitch in [(1.5,0),(.75,0),(1.5,1),(.75,1)]:
        target=temp/f'{rate}-{pitch}.wav'
        subprocess.run(['build/port/AudioRateTests',str(source),str(target),str(rate),str(pitch)],check=True)
        with wave.open(str(target),'rb') as audio:
            frames=audio.getnframes();samples=struct.unpack('<'+'h'*frames*2,audio.readframes(frames))[::2]
        duration=frames/44100;middle=samples[4410:-4410]
        crossings=sum(a<=0<b for a,b in zip(middle,middle[1:]));frequency=crossings/(len(middle)/44100)
        expected=440*(rate if pitch else 1)
        assert abs(duration-3/rate)<.1,(rate,pitch,duration)
        assert abs(frequency-expected)<4,(rate,pitch,frequency)
        print(f'PASS: {rate}x pitch={pitch}: {duration:.3f}s, {frequency:.1f}Hz')
