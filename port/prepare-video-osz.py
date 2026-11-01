#!/usr/bin/env python3
"""Convert every osu! video event in an .osz for iPod touch 2G playback."""
import argparse
import pathlib
import re
import shutil
import subprocess
import tempfile
import zipfile

VIDEO = re.compile(r'^(?P<prefix>\s*(?:Video|1)\s*,\s*[^,]+\s*,\s*)(?P<quote>"?)(?P<name>[^"\r\n,]+)(?P=quote)(?P<tail>.*)$', re.I)

def safe_extract(archive, root):
    root = root.resolve()
    for item in archive.infolist():
        target = (root / item.filename).resolve()
        if root not in target.parents and target != root:
            raise ValueError("archive path escapes extraction directory: " + item.filename)
        archive.extract(item, root)

def convert(source, destination):
    subprocess.run([
        "ffmpeg", "-y", "-i", str(source), "-an",
        "-vf", "scale=480:320:force_original_aspect_ratio=increase,crop=480:320",
        "-r", "30", "-c:v", "libx264", "-profile:v", "baseline", "-level", "3.0",
        "-pix_fmt", "yuv420p", "-b:v", "800k", "-maxrate", "1200k", "-bufsize", "2400k",
        "-movflags", "+faststart", str(destination)
    ], check=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    args = parser.parse_args()
    if not shutil.which("ffmpeg"):
        raise SystemExit("ffmpeg is required (install it on the Mac, not the iPod)")
    with tempfile.TemporaryDirectory(prefix="osu-video-") as temporary:
        root = pathlib.Path(temporary)
        with zipfile.ZipFile(args.input) as archive:
            safe_extract(archive, root)
        conversions = {}
        for osu in root.rglob("*.osu"):
            data = osu.read_text(encoding="utf-8-sig", errors="strict")
            lines = []
            for line in data.splitlines(keepends=True):
                ending = "\r\n" if line.endswith("\r\n") else "\n" if line.endswith("\n") else ""
                body = line[:-len(ending)] if ending else line
                match = VIDEO.match(body)
                if not match:
                    lines.append(line); continue
                relative = match.group("name").replace("\\", "/")
                source = (osu.parent / relative).resolve()
                if root.resolve() not in source.parents or not source.is_file():
                    lines.append(line); continue
                destination = source.with_name(source.stem + "-ipod.mp4")
                key = (source, destination)
                if key not in conversions:
                    convert(source, destination); conversions[key] = True
                new_relative = destination.relative_to(osu.parent).as_posix()
                lines.append(match.group("prefix") + '"' + new_relative + '"' + match.group("tail") + ending)
            osu.write_text("".join(lines), encoding="utf-8", newline="")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as archive:
            for item in root.rglob("*"):
                if item.is_file(): archive.write(item, item.relative_to(root))
        print("prepared %d video file(s): %s" % (len(conversions), args.output))

if __name__ == "__main__":
    main()
