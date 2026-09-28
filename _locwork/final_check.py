# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import json,os,re,sys
STR={s['id']:s for s in json.load(open(config.STRINGS_JSON,encoding='utf-8'))['strings']}
def final_tx(L):
    m={}
    for g in ['g1','g2','g3','g4','g5']:
        p=os.path.join(W,'out_data',L,'final',g+'.json')
        if os.path.exists(p):
            for r in json.load(open(p,encoding='utf-8'))['results']: m[int(r['id'])]=r.get('tx','')
    return m
de=final_tx('de')
CJK=re.compile(r'[　-ヿ㐀-䶿一-鿿]')
grand=0
for L in ['de','id','ja','pl','ru','tl','uk']:
    tx=final_tx(L); fffd=empty=gap=wrongscript=0; det=[]
    for i,s in STR.items():
        en=s['en']; t=tx.get(i,'')
        if '\ufffd' in t: fffd+=1
        if re.search(r'[A-Za-z]',en) and not t.strip(): empty+=1
        if L!='ja' and CJK.search(t): wrongscript+=1
        if re.search(r'[A-Za-z]{2,}',en) and t.strip().casefold()==en.strip().casefold():
            dt=de.get(i,'')
            if dt.strip().casefold()!=en.strip().casefold(): gap+=1; det.append(i)
    n=len(tx)
    print('%-3s n=%d fffd=%d empty=%d cjk-in-nonja=%d realgap=%d'%(L,n,fffd,empty,wrongscript,gap), det[:8])
    grand+=fffd+empty+wrongscript+gap
print('GRAND DEFECTS',grand)
