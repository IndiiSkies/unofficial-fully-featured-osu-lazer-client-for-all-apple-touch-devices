#!/usr/bin/env python3
"""Exercise the exact C decoder against FFmpeg-generated audio fixtures."""
import array
import math
import pathlib
import subprocess
import tempfile
import wave

root = pathlib.Path(__file__).resolve().parents[2]
host = root / 'build/ffmpeg-host'
source = root / 'build/ffmpeg-source'
binary = host / 'AudioDecodeTests'
subprocess.run(['clang', '-O2', '-I' + str(host), '-I' + str(source),
                str(root / 'port/Native/AudioDecode.c'), str(root / 'port/Native/AppleAudio.c'), str(root / 'port/Native/ImportGate.c'), str(root / 'port/Tests/AudioDecodeTests.c'),
                str(host / 'libavformat/libavformat.a'), str(host / 'libavcodec/libavcodec.a'),
                str(host / 'libswresample/libswresample.a'), str(host / 'libavutil/libavutil.a'),
                '-framework', 'CoreFoundation', '-framework', 'AudioToolbox', '-framework', 'Security', '-liconv', '-lz', '-lbz2', '-lm', '-o', str(binary)], check=True)
with tempfile.TemporaryDirectory(prefix='osu-audio-test-') as folder:
    folder = pathlib.Path(folder)
    formats = [('ogg', 'vorbis'), ('opus', 'opus'), ('flac', 'flac'), ('mp3', 'libmp3lame'), ('m4a', 'aac'), ('wma', 'wmav2'), ('wav', 'pcm_s24le')]
    for extension, codec in formats:
        encoded = folder / ('tone.' + extension)
        subprocess.run(['ffmpeg', '-v', 'error', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000:duration=1',
                        '-ac', '2', '-c:a', codec, '-strict', '-2', str(encoded)], check=True)
        decoded = folder / (extension + '-decoded.wav')
        subprocess.run([str(binary), str(encoded), str(decoded)], check=True)
        if extension in ('mp3', 'm4a', 'wav'):
            assert subprocess.run([str(binary), '--apple', str(encoded)]).returncode == 0, extension
        with wave.open(str(decoded), 'rb') as audio:
            assert audio.getnchannels() == 2 and audio.getsampwidth() == 2 and audio.getframerate() == 44100, extension
            assert .90 < audio.getnframes() / 44100 < 1.15, extension
            values = array.array('h', audio.readframes(audio.getnframes()))
            rms = math.sqrt(sum(float(v) * v for v in values) / len(values))
            assert 100 < rms < 12000, (extension, rms)
        print('PASS:', extension, 'decoded to stereo 44.1 kHz PCM with correct duration and signal')
    invalid = folder / 'invalid.ogg'
    invalid.write_bytes(b'not an audio stream')
    assert subprocess.run([str(binary), '--apple', str(invalid)]).returncode != 0
    destination = folder / 'invalid.wav'
    assert subprocess.run([str(binary), str(invalid), str(destination)], stderr=subprocess.DEVNULL).returncode != 0
    assert not destination.exists()
    print('PASS: invalid input fails without leaving a partial output')
