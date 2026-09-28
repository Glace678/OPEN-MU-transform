# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Consolidate every salvageable translation artifact into per-language master maps."""
import ast, json, os, glob

WORK = W
LANGS = ['de','id','ja','pl','ru','tl','uk']
CHUNKS = ['g1','g2','g3a','g3b','g4','g5']

def repair(s):
    out=[]; i=0
    while i < len(s):
        if ord(s[i]) < 128:
            out.append(s[i]); i+=1; continue
        j=i; run=[]
        while j < len(s) and ord(s[j])>=128:
            try: s[j].encode('gbk')
            except UnicodeEncodeError: break
            run.append(s[j]); j+=1
        if run:
            seg=''.join(run); rec=None
            try: rec=seg.encode('gbk').decode('utf-8')
            except Exception:
                for cut in range(len(run)-1,0,-1):
                    try:
                        rec=''.join(run[:cut]).encode('gbk').decode('utf-8')
                        j=i+cut; break
                    except Exception: continue
            out.append(rec if rec is not None else seg)
            i=j
        else:
            out.append(s[i]); i+=1
    return ''.join(out)

def load_json_map(path):
    data=json.load(open(path,encoding='utf-8')); out={}
    if isinstance(data,dict) and 'results' in data:
        for r in data['results']:
            if r.get('tx') is not None and str(r['tx']).strip():
                out[int(r['id'])]=str(r['tx'])
    elif isinstance(data,dict):
        for k,v in data.items():
            if isinstance(v,str) and str(k).lstrip('-').isdigit(): out[int(k)]=v
    elif isinstance(data,list):
        for r in data:
            if isinstance(r,dict) and 'id' in r and r.get('tx'): out[int(r['id'])]=str(r['tx'])
    return out

def extract_py_map(path):
    src=open(path,encoding='utf-8').read()
    tree=ast.parse(src); maps={}
    for node in ast.walk(tree):
        if isinstance(node,ast.Assign) and isinstance(node.value,ast.Dict):
            for k,v in zip(node.value.keys,node.value.values):
                if isinstance(v,ast.Constant) and isinstance(v.value,str):
                    try:
                        kk=str(ast.literal_eval(k))
                        maps.setdefault('_d',{})[kk]=v.value
                    except Exception: pass
        if isinstance(node,ast.Assign) and isinstance(node.value,ast.Constant) \
           and isinstance(node.value.value,str):
            for t in node.targets:
                if isinstance(t,ast.Subscript) and isinstance(t.value,ast.Name):
                    sl=t.slice
                    key=sl.value if isinstance(sl,ast.Constant) else None
                    if key is not None:
                        maps.setdefault(t.value.id,{})[str(key)]=node.value.value
    out={}
    for d in maps.values(): out.update(d)
    return {int(k):v for k,v in out.items() if str(k).lstrip('-').isdigit()}

SRC=[]
def add(lang,*pats):
    for pat in pats:
        for p in sorted(glob.glob(os.path.join(WORK,pat))):
            SRC.append((lang,p))

add('de','out_data/de/g1.json','out_data/de/g2.json')
add('id','out_data/id/g2.json','out_data/id/_batch3_partial.json','out_data/id/_batch4_partial.json',
    'out_data/id/_trans_batch3.py','out_data/id/_translate_batch4.py',
    '_translate_batch3_p1.py','_translate_batch3_p2.py','_translate_batch3_p3.py')
add('ja','out_data/ja/g1.json','tmp_out_0.json','tmp_out_1.json','tmp_out_2.json','tmp_out_3.json',
    'tmp_out_4.json','tmp_out_5.json','tmp_out_6.json')
add('pl','out_data/pl/g1.json')
add('ru','out_data/ru/g2.json','work/g1_batch2_out.json')
add('uk','out_data/uk/g1.json','out_data/uk/batch1_out.json','out_data/uk/batch2_out.json')

masters={l:{} for l in LANGS}
for lang,p in SRC:
    m=extract_py_map(p) if p.endswith('.py') else load_json_map(p)
    if lang!='ja': m={k:repair(v) for k,v in m.items()}
    before=len(masters[lang])
    for k,v in m.items(): masters[lang].setdefault(k,v)
    print(f"[{lang}] +{os.path.relpath(p,WORK):40s} {len(m):5d} (master {before}->{len(masters[lang])})")

m=masters['uk']
if 1517 in m:
    m[1517]=m[1517].replace('Без道德не','Аморальне').replace('без道德не','аморальне')

grand={}
for lang in LANGS:
    os.makedirs(os.path.join(WORK,'out_data',lang),exist_ok=True)
    with open(os.path.join(WORK,'out_data',lang,'_master.json'),'w',encoding='utf-8') as f:
        json.dump({str(k):v for k,v in sorted(masters[lang].items())},f,ensure_ascii=False,indent=0)
    print(f"\n== {lang}: master {len(masters[lang])}/3969")
    miss_all=[]
    for g in CHUNKS:
        d=json.load(open(os.path.join(WORK,'in_data',lang,g+'.json'),encoding='utf-8'))
        want=[s['id'] for s in d['strings']]
        miss=[i for i in want if i not in masters[lang]]
        miss_all+=miss
        print(f"   {g:4s} {len(want)-len(miss)}/{len(want)}"+(f"  missing {len(miss)}" if miss else "  OK"))
    grand[lang]=miss_all
    with open(os.path.join(WORK,'out_data',lang,'_missing.json'),'w',encoding='utf-8') as f:
        json.dump(miss_all,f)
print("\ntotal remaining:",sum(len(v) for v in grand.values()))
for l,v in grand.items(): print(l,len(v))
