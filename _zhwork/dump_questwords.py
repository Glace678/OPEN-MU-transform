# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import sys, os
sys.path.insert(0, config.LOC)
from bmd import read_questwords
import formats, opencc, re
t2s = opencc.OpenCC('t2s')
qw, _ = read_questwords(os.path.join(formats.CHS, 'QuestWords_chs.bmd'))
out = open(os.path.join(W, 'questwords_audit.txt'), 'w', encoding='utf-8')
n = 0
for idx, txt in qw:
    try:
        s = txt.decode('utf-8')
        bad_decode = False
    except UnicodeDecodeError:
        s = txt.decode('utf-8', errors='replace'); bad_decode = True
    if not s.strip():
        continue
    flags = []
    if bad_decode or '�' in s: flags.append('ENCODING')
    if t2s.convert(s) != s: flags.append('TRAD')
    if re.search(r'[가-힣]', s): flags.append('KOREAN')
    if flags:
        n += 1
        out.write(f'--- rec {idx} {flags}\n{s[:400]}\n\n')
out.write(f'TOTAL {n}\n')
out.close()
print('flagged:', n, 'of', len(qw))
