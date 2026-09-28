# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Propagate corrected English (FFFD repaired) into in_data and todo files;
repair FFFD in existing master translations by positional alignment."""
import json, os, glob

WORK = W
STR = {s['id']: s for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings']}

# 1) patch inputs
n=0
for p in glob.glob(os.path.join(WORK,'in_data','**','*.json'),recursive=True)+glob.glob(os.path.join(WORK,'todo','*.json')):
    d=json.load(open(p,encoding='utf-8'))
    if 'strings' not in d: continue
    changed=False
    for s in d['strings']:
        canon=STR.get(s['id'])
        if canon and s['en']!=canon['en']:
            s['en']=canon['en']; changed=True
    if changed:
        json.dump(d,open(p,'w',encoding='utf-8'),ensure_ascii=False,indent=1); n+=1
print('patched input files:',n)

# 2) repair master translations: replace FFFD runs positionally using corrected en
def repair_tx(en, tx):
    if '�' not in tx: return tx, False
    # tokenize en into literal segments split on runs of non-ascii punctuation
    # Strategy: map each maximal FFFD run in tx to the corresponding run in en.
    out=[]; ti=0; changed=False
    # Walk en and tx in parallel over ASCII anchors; between anchors, tx has � runs.
    import re
    # split en into tokens: ascii text chunks and special-punct chunks
    etoks=[(t, bool(re.fullmatch(r'[^ -~]+', t))) for t in re.findall(r'[^ -~]+|[ -~]+', en)]
    ttoks=re.findall(r'�+|[^�]+', tx)
    res=[]; ei=0
    for tt in ttoks:
        if set(tt)=={'�'}:
            # find next non-ascii special chunk in etoks (skip ascii chunks that tx rendered inline elsewhere)
            while ei<len(etoks) and not etoks[ei][1]:
                # ascii chunk: should already appear inside surrounding tx text; skip
                ei+=1
            if ei<len(etoks):
                res.append(etoks[ei][0]); ei+=1; changed=True
            else:
                res.append('…'); changed=True
        else:
            res.append(tt)
    return ''.join(res), changed

for lang in ['de','id','ja','pl','ru','tl','uk']:
    mp=os.path.join(WORK,'out_data',lang,'_master.json')
    if not os.path.exists(mp): continue
    d=json.load(open(mp,encoding='utf-8'))
    c=0
    for k,t in list(d.items()):
        if '�' in t:
            en=STR[int(k)]['en']
            nt,ch=repair_tx(en,t)
            if ch and '�' not in nt: d[k]=nt; c+=1
            else: print('  STILL BAD',lang,k,repr(t[:60]))
    json.dump(d,open(mp,'w',encoding='utf-8'),ensure_ascii=False,indent=0)
    print(lang,'master fffd repaired:',c)
