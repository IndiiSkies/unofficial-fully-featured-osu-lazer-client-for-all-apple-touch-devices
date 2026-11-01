"""Exercise the actual native extractor and managed publication/deduplication."""
from pathlib import Path
import ctypes
import hashlib
import shutil
import struct
import subprocess
import zipfile

root = Path(__file__).resolve().parents[2]
out = root / 'build/osz-import-tests'
if out.exists():
    shutil.rmtree(out)
out.mkdir(parents=True)
mini = root / 'mono-legacy/support/minizip'
library = out / 'libosz.dylib'
subprocess.run(['clang', '-dynamiclib', '-O2', '-g', '-DNOUNCRYPT', '-Wno-deprecated-declarations',
                '-I' + str(mini), str(root / 'port/Native/OszImport.c'), str(root / 'port/Native/ImportGate.c'), str(mini / 'unzip.c'), str(mini / 'ioapi.c'),
                '-lz', '-o', str(library)], check=True)
dll = ctypes.CDLL(str(library))
dll.legacy_osz_extract.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_void_p, ctypes.c_int]
dll.legacy_osz_hash.argtypes = [ctypes.c_char_p, ctypes.c_void_p, ctypes.c_int]
checks = 0

def check(ok, message):
    global checks
    checks += 1
    if not ok:
        raise AssertionError(message)

def archive(name, entries, compression=zipfile.ZIP_DEFLATED):
    path = out / name
    with zipfile.ZipFile(path, 'w', compression=compression) as z:
        for key, value in entries:
            z.writestr(key, value)
    return path

def extract(path, success=True):
    stage = out / ('stage-' + path.stem)
    stage.mkdir()
    error = ctypes.create_string_buffer(256)
    result = dll.legacy_osz_extract(bytes(path), bytes(stage), error, len(error))
    check((result == 0) == success, str(path.name) + ': ' + error.value.decode())
    return stage

beatmap = b'osu file format v14\n[General]\nAudioFilename: audio.wav\nMode: 0\n[Metadata]\nTitle: Archive test\nArtist: Test\nVersion: Normal\n[HitObjects]\n'
good = archive('valid.osz', [('./', b''), ('Song/map.osu', beatmap), ('Song/audio.wav', b'RIFF' + bytes(range(256)) * 300),
                             ('Song/背景.jpg', b'background'), ('Song/empty.txt', b'')])
stage = extract(good)
check((stage / 'Song/map.osu').read_bytes() == beatmap, 'Beatmap bytes preserved')
check((stage / 'Song/audio.wav').stat().st_size == 76804, 'Streaming audio output')
check((stage / 'Song/背景.jpg').read_bytes() == b'background', 'UTF-8 asset filename')
check((stage / 'Song/empty.txt').stat().st_size == 0, 'Empty files')
stored = archive('stored.osz', [('map.OSU', beatmap), ('audio.wav', b'audio')], zipfile.ZIP_STORED)
extract(stored)
windows = archive('windows.osz', [('folder\\map.osu', beatmap), ('folder\\audio.wav', b'audio')])
check((extract(windows) / 'folder/map.osu').exists(), 'Windows separators')
for i, name in enumerate(['../outside', '/tmp/outside', 'folder/../../outside', 'C:\\outside', '\\outside']):
    extract(archive('traversal-' + str(i) + '.osz', [(name, b'x')]), False)
link = zipfile.ZipInfo('linked')
link.create_system = 3
link.external_attr = 0o120777 << 16
extract(archive('symlink.osz', [(link, b'../outside')]), False)
extract(archive('duplicate.osz', [('same', b'a'), ('same', b'b')]), False)
bad = archive('crc.osz', [('map.osu', beatmap)], zipfile.ZIP_STORED)
data = bytearray(bad.read_bytes())
name_length, extra_length = struct.unpack_from('<HH', data, 26)
data[30 + name_length + extra_length] ^= 1
bad.write_bytes(data)
extract(bad, False)
truncated = out / 'truncated.osz'
truncated.write_bytes(good.read_bytes()[:100])
extract(truncated, False)
large = archive('oversized.osz', [('huge', b'x')])
data = bytearray(large.read_bytes())
central = data.index(b'PK\x01\x02')
struct.pack_into('<I', data, central + 24, 600 * 1024 * 1024)
large.write_bytes(data)
extract(large, False)
encrypted = archive('encrypted.osz', [('secret', b'x')])
data = bytearray(encrypted.read_bytes())
central = data.index(b'PK\x01\x02')
struct.pack_into('<H', data, central + 8, 1)
encrypted.write_bytes(data)
extract(encrypted, False)
digest = ctypes.create_string_buffer(65)
check(dll.legacy_osz_hash(bytes(good), digest, len(digest)) == 0 and digest.value.decode() == hashlib.sha256(good.read_bytes()).hexdigest(), 'SHA-256 fingerprint')
import threading
interrupt_stage = out / 'interrupt-stage'
interrupt_stage.mkdir()
entered = threading.Event()
interrupted = []
dll.legacy_import_pause(1)
def interrupted_extract():
    dll.legacy_import_worker_register()
    dll.legacy_import_begin_work()
    entered.set()
    error = ctypes.create_string_buffer(256)
    interrupted.append(dll.legacy_osz_extract(bytes(good), bytes(interrupt_stage), error, len(error)))
worker = threading.Thread(target=interrupted_extract)
worker.start()
assert entered.wait(5)
dll.legacy_import_interrupt()
dll.legacy_import_pause(0)
worker.join(5)
check(not worker.is_alive() and interrupted == [-2], 'Native extraction yields distinctly from corrupt-archive failure')
archive('no-maps.osz', [('audio.wav', b'audio')])
for title in ('Queue A', 'Queue B', 'Queue C'):
    archive(title + '.osz', [('map.osu', beatmap.replace(b'Archive test', title.encode())), ('audio.wav', b'fixture')])

exe = out / 'ImportTests.exe'
subprocess.run(['mcs', '-nostdlib', '-r:' + str(root / 'mono-legacy/mcs/class/lib/net_4_0/mscorlib.dll'),
                '-r:' + str(root / 'build/port/OsuLegacy.app/managed/OsuLegacy.Core.dll'), '-out:' + str(exe), str(root / 'port/Managed/OszImporter.cs'), str(root / 'port/Managed/MenuPlaylist.cs'), str(root / 'port/Managed/LocalScores.cs'), str(root / 'port/Managed/LazerUi.cs'), str(root / 'port/Tests/ImportTests.cs')], check=True)
(out / 'ImportTests.exe.config').write_text('<configuration><dllmap dll="__Internal" target="' + str(library) + '"/></configuration>')
import os
env = dict(os.environ, MONO_PATH=str(root / 'build/port/OsuLegacy.app/managed'))
subprocess.run(['mono', str(exe), str(out)], check=True, env=env)
print('PASS: ' + str(checks) + ' native archive checks')
