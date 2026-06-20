import py7zr
import os
import sys

src = sys.argv[1]
dest = sys.argv[2]

print(f"Extracting {src} -> {dest}")
os.makedirs(dest, exist_ok=True)

with py7zr.SevenZipFile(src, 'r') as z:
    names = z.getnames()
    print(f"Total entries: {len(names)}")
    safe = [n for n in names if not (n.endswith('libcrypto.dylib') or n.endswith('libssl.dylib'))]
    print(f"After excluding symlinks: {len(safe)}")
    z.extract(path=dest, targets=safe)

print("Done")
