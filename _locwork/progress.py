# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os, glob
WORK=W
todo=os.path.join(WORK,'todo')
man=json.load(open(os.path.join(todo,'_manifest.json'),encoding='utf-8'))
grand=0; gdone=0
for lang, batches in man.items():
    done=[]; partial=[]
    for name,n in batches:
        out=os.path.join(WORK,'out_data',lang,name+'.json')
        if os.path.exists(out):
            try:
                r=json.load(open(out,encoding='utf-8'))['results']
                have=len({x['id'] for x in r if x.get('tx','').strip()})
                if have>=n: done.append(name)
                else: partial.append((name,have,n))
            except Exception as e: partial.append((name,'ERR',str(e)[:30]))
    tot=sum(n for _,n in batches)
    dn =sum(n for nm,n in batches if nm in done)
    grand+=tot; gdone+=dn
    print(f'{lang}: {dn}/{tot}  done={len(done)}/{len(batches)} partial={partial}')
print(f'TOTAL {gdone}/{grand} = {gdone*100//grand}%')
