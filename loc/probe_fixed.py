# -*- coding: utf-8 -*-
# Usage: python probe_fixed.py <path> <R> <N> <keyHex|-> [dumpCount]
import sys, bmd

path = sys.argv[1]
R = int(sys.argv[2]); N = int(sys.argv[3])
key = None if sys.argv[4] in ('-', 'none') else int(sys.argv[4], 16)
dumpn = int(sys.argv[5]) if len(sys.argv) > 5 else 4

f = bmd.FixedFile(path, R, N, key=key)
v = f.verify()
raw = open(path, 'rb').read()
rt = f.encode()
print(f'{path}')
print(f'  R={R} N={N} key={hex(key) if key else None} filesize={len(raw)} expected={R*N+4}')
print(f'  checksum: {v}')
print(f'  round-trip identical: {rt == raw}')

# find non-empty records (record has any ASCII letter in first R)
shown = 0
for i, r in enumerate(f.recs):
    if any(65 <= b < 127 for b in r):
        print(f'  --- rec {i} ---')
        for base in range(0, R, 16):
            chunk = r[base:base+16]
            hexs = ' '.join('%02x' % b for b in chunk)
            asc = ''.join(chr(b) if 32 <= b < 127 else '.' for b in chunk)
            print('   %4d  %-47s  %s' % (base, hexs, asc))
        shown += 1
        if shown >= dumpn:
            break
