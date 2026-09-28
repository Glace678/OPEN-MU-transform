# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
import json, os, subprocess, sys
WORK=W
man=json.load(open(os.path.join(WORK,'todo','_manifest.json'),encoding='utf-8'))
py=sys.executable
bad=0; ok=0
for lang,batches in man.items():
    for name,n in batches:
        inp=os.path.join(WORK,'todo',name+'.json')
        out=os.path.join(WORK,'out_data',lang,name+'.json')
        if not os.path.exists(out): continue
        r=subprocess.run([py,os.path.join(WORK,'validate_batch.py'),inp,out],capture_output=True,text=True,encoding='utf-8')
        if r.returncode==0: ok+=1
        else:
            bad+=1; print('FAIL',name); print(r.stdout.strip()[:600])
print(f'validated ok={ok} fail={bad}')
