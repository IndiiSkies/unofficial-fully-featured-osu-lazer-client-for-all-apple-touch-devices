"""Create a reproducible .osz so the bundled demo exercises real import."""
from pathlib import Path
import sys
import zipfile

source, destination = map(Path, sys.argv[1:])
with zipfile.ZipFile(destination, 'w') as archive:
    for path in sorted(source.iterdir()):
        if not path.is_file():
            continue
        info = zipfile.ZipInfo(path.name, date_time=(2000, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        archive.writestr(info, path.read_bytes())
