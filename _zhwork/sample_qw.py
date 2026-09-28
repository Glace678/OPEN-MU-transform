# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import sys, os
sys.path.insert(0, config.LOC)
from bmd import read_questwords
import formats
qw, _ = read_questwords(os.path.join(formats.CHS, 'QuestWords_chs.bmd'))
out = open(os.path.join(W, 'qw_samples.txt'), 'w', encoding='utf-8')
shown = 0
for idx, txt in qw:
    try:
        s = txt.decode('utf-8')
    except UnicodeDecodeError:
        continue
    if ('接受' in s or '拒绝' in s or '级' in s) and 5 < len(s) < 120:
        out.write(f'{idx}: {s}\n')
        shown += 1
        if shown >= 15:
            break
out.close()
print('ok')
