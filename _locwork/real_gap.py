# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os, re
from collections import Counter
STR={s['id']:s for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings']}
def final_tx(L):
    m={}
    for g in ['g1','g2','g3','g4','g5']:
        p=os.path.join(W,'out_data',L,'final',g+'.json')
        if os.path.exists(p):
            for r in json.load(open(p,encoding='utf-8'))['results']: m[int(r['id'])]=r.get('tx','')
    return m
de=final_tx('de')
def kept(L,i):
    tx=final_tx(L)
    return tx
real={}
for L in ['id','pl','tl','ja','ru','uk']:
    tx=final_tx(L)
    ids=[]
    for i,s in STR.items():
        en=s['en']
        if not re.search(r'[A-Za-z]{2,}',en): continue
        mine=tx.get(i,'')
        if mine and mine.strip().casefold()==en.strip().casefold():
            # German translated it? => real gap; German also kept => proper noun, leave
            de_tx=de.get(i,'')
            de_translated = de_tx.strip() and de_tx.strip().casefold()!=en.strip().casefold()
            if de_translated:
                ids.append(i)
    real[L]=sorted(ids)
    print(L,'REAL gap (de translated, this kept):',len(ids),Counter(STR[i]['kinds'][0] for i in ids))
json.dump(real,open(os.path.join(W,'real_gap_ids.json'),'w'))
