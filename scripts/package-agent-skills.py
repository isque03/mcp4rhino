#!/usr/bin/env python3
"""Package canonical agent skills; optionally generate the repository's Cursor copy."""
import argparse
from pathlib import Path
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def package(cursor=False):
    source = ROOT / 'skills' / 'grasshopper-parametric-design'
    version = re.search(r'^version:\s*(\S+)', (ROOT / 'manifest.yml').read_text(), re.M)[1]
    destination = ROOT / 'dist' / f'MCP4Rhino-{version}-skills.zip'
    destination.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED) as archive:
        for file in sorted(source.rglob('*')):
            if file.is_file():
                archive.write(file, file.relative_to(source.parent))
    if cursor:
        shutil.copytree(source, ROOT / '.cursor' / 'skills' / source.name, dirs_exist_ok=True)
    print(destination)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cursor', action='store_true', help='Generate .cursor/skills copy from canonical sources')
    package(parser.parse_args().cursor)
