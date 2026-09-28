# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
import bmd
f = bmd.FixedFile(os.path.join(config.SERVER.chs, 'item_chs.bmd'), 84, 8192, key=0xE2F1)
maxlen = 0; maxname = ''; n_twohand = 0; n_nonempty = 0; name_over29 = 0
off30 = set()
for i, r in enumerate(f.recs):
    end = r.find(b'\x00', 0, 34)
    nm = bytes(r[:end if end >= 0 else 30])
    if nm.strip(b'\x00'):
        n_nonempty += 1
        L = end
        if L > maxlen:
            maxlen = L; maxname = nm.decode('latin1')
        if L > 29:
            name_over29 += 1
    off30.add(r[30])
    if r[30] == 1:
        n_twohand += 1
print('nonempty items:', n_nonempty)
print('max name bytes:', maxlen, repr(maxname))
print('names with nul beyond offset29:', name_over29)
print('offset30 distinct values:', sorted(off30), 'count(r[30]==1)=', n_twohand)
for i, r in enumerate(f.recs):
    if r[30] == 1:
        print('twohand example rec', i, bytes(r[:22])); break
