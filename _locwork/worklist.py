# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os
STR={int(k):v for k,v in ((s['id'],s) for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings'])}
real=json.load(open(os.path.join(W,'real_gap_ids.json')))
real={k:set(v) for k,v in real.items()}
union=sorted(set(real['id'])|set(real['tl'])|set(real['pl'])|set(real['ja']))
order={'item':0,'skill':1,'buff':2,'setoption':3,'mastertooltip':4,'questwords':5,'movereq':6,'npcname':7,'minimap':8}
union.sort(key=lambda i:(order.get(STR[i]['kinds'][0],9),STR[i]['en']))
wl=[{'id':i,'en':STR[i]['en'],'budget':STR[i]['budget'],'kinds':STR[i]['kinds'],
     'langs':[L for L in ['id','tl','pl','ja'] if i in real[L]]} for i in union]
json.dump(wl,open(os.path.join(W,'gap_worklist.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=0)
from collections import Counter
print('union strings',len(wl),Counter(x['kinds'][0] for x in wl))
