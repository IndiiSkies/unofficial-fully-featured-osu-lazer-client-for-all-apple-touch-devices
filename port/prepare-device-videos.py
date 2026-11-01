#!/usr/bin/env python3
"""Prepare imported beatmap videos on the Mac and upload compatible companions.

Requires ffmpeg, ffprobe and build/device-documents. Original files and beatmap
events are preserved; the player prefers <original filename>.ipod.mp4.
"""
import argparse
import hashlib
import json
import pathlib
import subprocess
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent

def validate(path):
    data = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_streams", "-of", "json", str(path)], text=True))
    streams = data["streams"]
    if len(streams) != 1:
        raise ValueError("Expected a silent video-only file")
    video = streams[0]
    if video["codec_name"] != "h264" or "Baseline" not in video["profile"] or (video["width"], video["height"]) != (480, 320) or video["pix_fmt"] != "yuv420p" or video["level"] > 30:
        raise ValueError("Video does not match the iPod playback profile")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--udid", required=True)
    parser.add_argument("--downloads", type=pathlib.Path, default=pathlib.Path.home()/"Downloads")
    args = parser.parse_args()
    helper = ROOT/"build/device-documents"
    cache = ROOT/"build/video-cache"
    cache.mkdir(parents=True, exist_ok=True)
    def device(*command, capture=False):
        return subprocess.run([str(helper), args.udid, *map(str, command)], check=True,
                              text=True, stdout=subprocess.PIPE if capture else None).stdout
    videos = device("videos", "Documents/Beatmaps", capture=True).splitlines()
    archive_files = {}
    for archive in args.downloads.glob("*.osz"):
        try:
            with zipfile.ZipFile(archive) as zipped:
                for name in zipped.namelist():
                    if pathlib.PurePosixPath(name).suffix.lower() in (".mp4", ".m4v", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".mpg", ".mpeg"):
                        archive_files.setdefault(pathlib.PurePosixPath(name).name.casefold(), []).append((archive, name))
        except zipfile.BadZipFile:
            continue
    for remote in videos:
        key = hashlib.sha256(remote.encode()).hexdigest()
        source = cache/(key+pathlib.PurePosixPath(remote).suffix)
        matches = archive_files.get(pathlib.PurePosixPath(remote).name.casefold(), [])
        if len(matches) == 1:
            archive, name = matches[0]
            with zipfile.ZipFile(archive) as zipped:
                source.write_bytes(zipped.read(name))
        else:
            device("get", remote, source)
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        output = cache/(digest+"-480x320-baseline-v1.mp4")
        if not output.exists():
            partial = output.with_suffix(".partial.mp4")
            subprocess.run([
                "ffmpeg", "-v", "error", "-y", "-i", str(source), "-an",
                "-vf", "scale=480:320:force_original_aspect_ratio=increase,crop=480:320",
                "-r", "30", "-c:v", "libx264", "-preset", "veryfast", "-threads", "2",
                "-profile:v", "baseline", "-level:v", "3.0", "-pix_fmt", "yuv420p",
                "-crf", "22", "-maxrate", "1000k", "-bufsize", "2000k", "-g", "60",
                "-movflags", "+faststart", str(partial)], check=True)
            validate(partial)
            partial.replace(output)
        validate(output)
        destination = remote+".ipod.mp4"
        device("put", output, destination+".partial")
        device("rename", destination+".partial", destination)
        print("Prepared and uploaded:", remote, flush=True)
    print("Ready:", len(videos), "installed beatmap video(s)")

if __name__ == "__main__":
    main()
