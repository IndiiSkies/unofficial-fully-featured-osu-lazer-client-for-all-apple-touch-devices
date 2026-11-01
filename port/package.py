#!/usr/bin/env python3
"""Package our already signed app, keeping executable Unix permissions."""
from pathlib import Path
import sys
import zipfile

app, destination = map(Path, sys.argv[1:])
with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as output:
    for path in sorted(app.rglob("*")):
        if path.is_file():
            output.write(path, str(Path("Payload") / app.name / path.relative_to(app)))
print("Packaged", destination)
