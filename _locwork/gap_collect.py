# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os, re
STR={s['id']:s for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings']}
TRANSLATE_KINDS={'item','skill','buff','setoption','mastertooltip','questwords','harmony','socket'}
gap={}
for L in ['de','id','ja','pl','ru','tl','uk']:
    ids=set()
    for g in ['g1','g2','g3','g4','g5']:
        p=os.path.join(W,'out_data',L,'final',g+'.json')
        if not os.path.exists(p): continue
        for r in json.load(open(p,encoding='utf-8'))['results']:
            en=STR.get(r['id'],{}).get('en',''); tx=r.get('tx','')
            kinds=STR.get(r['id'],{}).get('kinds',[])
            if (tx and en and tx.strip().casefold()==en.strip().casefold()
                and re.search(r'[A-Za-z]{2,}',en)
                and any(k in TRANSLATE_KINDS for k in kinds)):
                ids.add(r['id'])
    gap[L]=sorted(ids)
    print(L,'retranslate ids:',len(ids))
json.dump(gap,open(os.path.join(W,'retranslate_ids.json'),'w'),indent=0)
# show kind breakdown
from collections import Counter
for L,ids in gap.items():
    c=Counter(STR[i]['kinds'][0] for i in ids)
    print(' ',L,dict(c))
