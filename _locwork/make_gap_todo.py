# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os
STR={s['id']:s for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings']}
gap=json.load(open(os.path.join(W,'retranslate_ids.json')))
LANGNAME={'de':'German','id':'Indonesian','ja':'Japanese','pl':'Polish','ru':'Russian','tl':'Tagalog/Filipino','uk':'Ukrainian'}
os.makedirs(os.path.join(W,'todo','gap'),exist_ok=True)
os.makedirs(os.path.join(W,'out_data_gap'),exist_ok=True)
BS=300
plan={}
for L,ids in gap.items():
    if not ids: continue
    os.makedirs(os.path.join(W,'out_data_gap',L),exist_ok=True)
    batches=[ids[i:i+BS] for i in range(0,len(ids),BS)]
    plan[L]=[]
    for pi,b in enumerate(batches,1):
        strings=[{'id':i,'en':STR[i]['en'],'budget':STR[i]['budget'],'kinds':STR[i]['kinds']} for i in b]
        out=os.path.join(W,'out_data_gap',L,f'{L}_gap_p{pi}.json').replace('\\','/')
        obj={'locale':L,'language':LANGNAME[L],'chunk':f'gap_p{pi}','out':out,'strings':strings}
        tp=os.path.join(W,'todo','gap',f'{L}_gap_p{pi}.json')
        json.dump(obj,open(tp,'w',encoding='utf-8'),ensure_ascii=False)
        plan[L].append(tp)
for L,ps in plan.items(): print(L,len(ps),'batches')
json.dump(plan,open(os.path.join(W,'gap_plan.json'),'w'),indent=1)
print('total batches',sum(len(v) for v in plan.values()))
