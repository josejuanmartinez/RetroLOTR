from pathlib import Path
import subprocess
import sys

files = [
    r'Assets\Art\Cards\Lands\IronHills.jpg',
    r'Assets\Art\Cards\Lands\Ithilien.jpg',
    r'Assets\Art\Cards\Lands\Khand.jpg',
    r'Assets\Art\Cards\Lands\Lindon.jpg',
    r'Assets\Art\Cards\Lands\Lothlorien.jpg',
    r'Assets\Art\Cards\Lands\MistyMountains.jpg',
    r'Assets\Art\Cards\Lands\NearHarad.jpg',
    r'Assets\Art\Cards\Lands\Nindalf.jpg',
    r'Assets\Art\Cards\Lands\NorthernGondor.jpg',
    r'Assets\Art\Cards\Lands\NorthernMirkwood.jpg',
    r'Assets\Art\Cards\Lands\Nurn.jpg',
    r'Assets\Art\Cards\Lands\Rhovanion.jpg',
    r'Assets\Art\Cards\Lands\Rhudaur.jpg',
    r'Assets\Art\Cards\Lands\Rhun.jpg',
    r'Assets\Art\Cards\Lands\Rivendell.jpg',
    r'Assets\Art\Cards\Lands\Rohan.jpg',
    r'Assets\Art\Cards\Lands\SouthernGondor.jpg',
    r'Assets\Art\Cards\Lands\SouthernMirkwood.jpg',
    r'Assets\Art\Cards\Lands\TheNorthDowns.jpg',
    r'Assets\Art\Cards\Lands\TheShire.jpg',
    r'Assets\Art\Cards\Lands\Udun.jpg',
    r'Assets\Art\Cards\Lands\Umbar.jpg',
    r'Assets\Art\Cards\Lands\Ungol.jpg',
    r'Assets\Art\Cards\Lands\WitheredHeath.jpg',
]

for f in files:
    print(f'=== {f} ===', flush=True)
    cmd = [
        sys.executable,
        r'.agents\skills\colorify\scripts\colorify_card.py',
        '--image', f,
        '--out', f,
        '--upload-max-dim', '512',
        '--force',
    ]
    result = subprocess.run(cmd)
    if result.returncode != 0:
        sys.exit(result.returncode)

print('DONE', flush=True)
