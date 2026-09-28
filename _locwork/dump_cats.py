# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os
wl=json.load(open(os.path.join(W,'gap_worklist.json'),encoding='utf-8'))
items=[x for x in wl if x['kinds'][0]=='item']
open(os.path.join(W,'_items_rest.txt'),'w',encoding='utf-8').write('\n'.join(x['en'] for x in items[520:]))
print('rest items',len(items)-520)
for cat in ['skill','buff','setoption','movereq','npcname','mastertooltip','minimap','questwords']:
    xs=[x for x in wl if x['kinds'][0]==cat]
    lines=['{}|{}|{}'.format(x['id'],x['budget'],x['en']) for x in xs]
    open(os.path.join(W,'_gap_%s.txt'%cat),'w',encoding='utf-8').write('\n'.join(lines))
    print(cat,len(xs))
