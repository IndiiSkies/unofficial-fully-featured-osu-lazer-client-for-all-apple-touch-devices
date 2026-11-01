#!/usr/bin/env python3
"""Prepare the supplied contest packs at the iPod's native landscape size."""
import pathlib
import subprocess
import sys

source, output = map(pathlib.Path, sys.argv[1:])
output.mkdir(parents=True, exist_ok=True)
manifest = []
for index, pack in enumerate(sorted(p for p in source.iterdir() if p.is_dir())):
    images = sorted(p for p in pack.iterdir() if p.suffix.lower() in ('.jpg', '.jpeg', '.png', '.webp'))
    for number, image in enumerate(images):
        name = 'wallpaper-%02d-%03d.jpg' % (index, number)
        subprocess.run(['magick', str(image), '-auto-orient', '-resize', '480x320^',
                        '-gravity', 'center', '-extent', '480x320', '-strip', '-quality', '84', str(output / name)], check=True)
        manifest.append(pack.name.strip() + '\t' + name + '\t' + image.name)
(output / 'wallpapers.tsv').write_text('\n'.join(manifest) + '\n', encoding='utf-8')
print('Prepared %d wallpapers from %d packs' % (len(manifest), index + 1))
