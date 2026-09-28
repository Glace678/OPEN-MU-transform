# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Merge master + every translated todo batch; validate full coverage;
emit canonical g1..g5 result files under out_data/<lang>/final/ for the builder."""
import json, os, glob, sys
sys.path.insert(0, W)
from validate_batch import phset, script_ok

WORK = W
LANGS = ['de','id','ja','pl','ru','tl','uk']
CHUNKS = ['g1','g2','g3a','g3b','g4','g5']
MERGE = {'g1':'g1','g2':'g2','g3':['g3a','g3b'],'g4':'g4','g5':'g5'}

def load_results(p):
    d=json.load(open(p,encoding='utf-8'))
    out={}
    if isinstance(d,dict) and 'results' in d:
        for r in d['results']:
            if r.get('tx') is not None and str(r['tx']).strip(): out[int(r['id'])]=str(r['tx'])
    return out

overall_ok=True
for lang in LANGS:
    master={int(k):v for k,v in json.load(open(os.path.join(WORK,'out_data',lang,'_master.json'),encoding='utf-8')).items()}
    merged=dict(master)
    batches={}
    for p in sorted(glob.glob(os.path.join(WORK,'out_data',lang,f'{lang}_*.json'))):
        m=load_results(p)
        for k,v in m.items(): merged.setdefault(k,v)
        batches[os.path.basename(p)]=len(m)
    # full validation against ALL in_data chunks
    problems=[]; total_ids=set()
    for chunk in CHUNKS:
        fp=os.path.join(WORK,'in_data',lang,chunk+'.json')
        if not os.path.exists(fp): continue
        d=json.load(open(fp,encoding='utf-8'))
        for s in d['strings']:
            total_ids.add(s['id'])
            i,en,bud=s['id'],s['en'],s['budget']
            t=merged.get(i)
            if t is None: problems.append((i,'missing')); continue
            if not t.strip(): problems.append((i,'empty'))
            if phset(en)!=phset(t): problems.append((i,'placeholder'))
            for ch,nm in [(';',';'),('#','#'),('/','/')]:
                if en.count(ch)!=t.count(ch): problems.append((i,nm))
            if len(t.encode('utf-8'))>bud: problems.append((i,f'budget {len(t.encode("utf-8"))}>{bud}'))
            if not script_ok(lang,t): problems.append((i,'script'))
    miss_ids=total_ids-set(merged)
    # emit canonical chunks
    od=os.path.join(WORK,'out_data',lang,'final'); os.makedirs(od,exist_ok=True)
    for gname,src in MERGE.items():
        srcs=src if isinstance(src,list) else [src]
        results=[]
        for chunk in srcs:
            d=json.load(open(os.path.join(WORK,'in_data',lang,chunk+'.json'),encoding='utf-8'))
            for s in d['strings']:
                results.append({'id':s['id'],'tx':merged.get(s['id'],'')})
        json.dump({'results':results},open(os.path.join(od,gname+'.json'),'w',encoding='utf-8'),ensure_ascii=False)
    status='OK' if not problems and len(merged)>=len(total_ids) else 'INCOMPLETE'
    if status!='OK': overall_ok=False
    print(f'== {lang}: merged {len(merged)}/{len(total_ids)} batches={len(batches)} -> {status}')
    if problems: print('   problems:',problems[:15], '...' if len(problems)>15 else '')
print('ALL LANGUAGES COMPLETE' if overall_ok else 'SOME LANGUAGES INCOMPLETE')
sys.exit(0 if overall_ok else 1)
