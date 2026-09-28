# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Emit self-contained translation worklists for ids missing from _master.json."""
import json, os

WORK = W
LANGS = ['de','id','ja','pl','ru','tl','uk']
CHUNKS = ['g1','g2','g3a','g3b','g4','g5']
LANGNAME = {
 'de':'German (Deutsch)','id':'Indonesian (Bahasa Indonesia)','ja':'Japanese (日本語)',
 'pl':'Polish (polski)','ru':'Russian (русский)','tl':'Tagalog/Filipino (Filipino)',
 'uk':'Ukrainian (українська)'}
BATCH = 320
TODO = os.path.join(WORK,'todo')
os.makedirs(TODO, exist_ok=True)

manifest = {}
total = 0
for lang in LANGS:
    master = json.load(open(os.path.join(WORK,'out_data',lang,'_master.json'),encoding='utf-8'))
    have = {int(k) for k in master}
    batches = []
    for chunk in CHUNKS:
        d = json.load(open(os.path.join(WORK,'in_data',lang,chunk+'.json'),encoding='utf-8'))
        miss = [s for s in d['strings'] if s['id'] not in have]
        if not miss: continue
        for pi in range(0, len(miss), BATCH):
            part = miss[pi:pi+BATCH]
            name = f"{lang}_{chunk}" if len(miss)<=BATCH else f"{lang}_{chunk}_p{pi//BATCH+1}"
            path = os.path.join(TODO, name+'.json')
            payload = {'locale':lang,'language':LANGNAME[lang],'chunk':chunk,
                       'out':os.path.join(WORK,'out_data',lang,name+'.json'),
                       'strings':[{'id':s['id'],'en':s['en'],'budget':s['budget'],'kinds':s['kinds']} for s in part]}
            json.dump(payload, open(path,'w',encoding='utf-8'), ensure_ascii=False, indent=0)
            batches.append((name,len(part))); total += len(part)
    manifest[lang]=batches

json.dump(manifest, open(os.path.join(TODO,'_manifest.json'),'w',encoding='utf-8'), ensure_ascii=False, indent=1)
for l,bs in manifest.items():
    print(l, len(bs),'batches', sum(n for _,n in bs),'strings')
    for n,k in bs: print('   ',n,k)
print('TOTAL batches', sum(len(v) for v in manifest.values()), 'strings', total)
