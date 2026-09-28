# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import xml.etree.ElementTree as ET, os, json
loc=config.LOCALIZATION
def data(g,l):
    p=os.path.join(loc,f'{g}.{l}.resx')
    root=ET.parse(p).getroot()
    out={}
    for d in root.findall('data'):
        k=d.get('name')
        v=d.find('value'); c=d.find('comment')
        out[k]=(v.text if v is not None else '', c.text if c is not None else None)
    return out
enG=data('Game','en'); enD=data('Dialog','en')
miss={}
for l in ['de','es','id','ja','pl','pt','ru','tl','uk']:
    G=data('Game',l); D=data('Dialog',l)
    items=[]
    for k in enG:
        if k not in G: items.append(('Game',k,enG[k][0],enG[k][1]))
    for k in enD:
        if k not in D: items.append(('Dialog',k,enD[k][0],enD[k][1]))
    # empties
    for k,v in G.items():
        if k in enG and not (v[0] or '').strip(): items.append(('Game-EMPTY',k,enG[k][0],enG[k][1]))
    miss[l]=items
for l,items in miss.items():
    print('='*80); print(l,len(items))
    for g,k,v,c in items:
        print(f'  [{g}] {k!r} = {v!r}' + (f'   /* {c} */' if c else ''))
json.dump({l:[list(x) for x in its] for l,its in miss.items()},
          open(os.path.join(W, 'resx_missing.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=1)
