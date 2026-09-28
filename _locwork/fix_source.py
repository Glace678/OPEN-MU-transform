# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Repair the 19 FFFD-corrupted English source strings from raw Eng BMD bytes."""
import json, re, sys
sys.path.insert(0,config.LOC)
from bmd import read_questwords

SP = config.STRINGS_JSON
obj = json.load(open(SP,encoding='utf-8'))
recs,_ = read_questwords(os.path.join(config.DEV_TREE, 'Data', 'Local', 'Eng', 'QuestWords_eng.bmd'))

def mixed_decode(bb):
    out=[]; i=0
    while i<len(bb):
        if bb[i]<0x80: out.append(chr(bb[i])); i+=1; continue
        dec=None; used=2
        for L in (3,2):
            try:
                cand=bb[i:i+L].decode('utf-8')
                if all(ord(c)>=0x80 for c in cand): dec=cand; used=L; break
            except Exception: pass
        if dec is None:
            dec=bb[i:i+2].decode('cp949','replace'); used=2
        out.append(dec); i+=used
    return ''.join(out)

def fuzzy(en):
    pat=b''
    for ch in en:
        pat += b'[\x80-\xff]+' if ch=='�' else re.escape(ch.encode('utf-8'))
    rx=re.compile(pat,re.S)
    hits=set()
    for idx,b in recs:
        bb=b.rstrip(b'\x00')
        m=rx.search(bb)
        if m: hits.add(mixed_decode(bb))
    return hits

fixed=0
for s in obj['strings']:
    if '�' not in s['en']: continue
    hits=fuzzy(s['en'])
    if len(hits)==1:
        new=next(iter(hits))
        assert '�' not in new
        s['en']=new; fixed+=1
        print('FIXED',s['id'],repr(new[:80]))
    else:
        print('AMBIGUOUS',s['id'],len(hits))
json.dump(obj,open(SP,'w',encoding='utf-8'),ensure_ascii=False,indent=1)
print('fixed',fixed)
