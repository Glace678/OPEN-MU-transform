# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Audit existing per-language master translations against ALL in_data chunks."""
import json, os, sys
sys.path.insert(0, W)
from validate_batch import phset, script_ok

WORK = W
LANGS = ['de','id','ja','pl','ru','tl','uk']
CHUNKS = ['g1','g2','g3a','g3b','g4','g5']

def audit(lang):
    mp=os.path.join(WORK,'out_data',lang,'_master.json')
    master={int(k):v for k,v in json.load(open(mp,encoding='utf-8')).items()} if os.path.exists(mp) else {}
    problems=[]
    for chunk in CHUNKS:
        fp=os.path.join(WORK,'in_data',lang,chunk+'.json')
        if not os.path.exists(fp): continue
        for s in json.load(open(fp,encoding='utf-8'))['strings']:
            i,en,bud=s['id'],s['en'],s['budget']
            if i not in master: continue
            t=master[i]; why=[]
            if phset(en)!=phset(t): why.append('placeholder')
            for ch,name in [(';','semicolon'),('#','hash'),('/','slash')]:
                if en.count(ch)!=t.count(ch): why.append(name)
            if len(t.encode('utf-8'))>bud: why.append(f'budget{len(t.encode("utf-8"))}>{bud}')
            if not script_ok(lang,t): why.append('wrong-script')
            if why: problems.append((i,chunk,why,en,t))
    return problems

allbad={}
for lang in LANGS:
    ps=audit(lang); allbad[lang]=[p[0] for p in ps]
    print(f"== {lang}: {len(ps)} defective")
    for i,chunk,why,en,t in ps[:20]:
        print(f"   {i:5d} {chunk:4s} {why}\n        EN {en[:80]}\n        TX {t[:80]}")
json.dump(allbad,open(os.path.join(WORK,'todo','_defective.json'),'w',encoding='utf-8'))
print('total defective:',sum(len(v) for v in allbad.values()))
