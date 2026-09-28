# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Merge out_data_gap/<L>/*.json translations into out_data/<L>/final/gN.json by id."""
import json, os, glob, sys
langs=sys.argv[1:] or ['de','id','ja','pl','ru','tl','uk']
for L in langs:
    gap={}
    for p in glob.glob(os.path.join(W,'out_data_gap',L,'*.json')):
        for r in json.load(open(p,encoding='utf-8'))['results']:
            t=(r.get('tx') or '').strip()
            if t: gap[int(r['id'])]=t
    if not gap:
        print(L,'no gap outputs'); continue
    tot=0
    for g in ['g1','g2','g3','g4','g5']:
        fp=os.path.join(W,'out_data',L,'final',g+'.json')
        if not os.path.exists(fp): continue
        d=json.load(open(fp,encoding='utf-8'))
        for r in d['results']:
            if int(r['id']) in gap:
                r['tx']=gap[int(r['id'])]; tot+=1
        json.dump(d,open(fp,'w',encoding='utf-8'),ensure_ascii=False)
    print(L,'gap tx loaded',len(gap),'applied to final',tot)
