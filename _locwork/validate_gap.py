# -*- coding: utf-8 -*-
import json,re,sys,os
sys.path.insert(0,W)
from gap_engine import W,STR
CJK=re.compile(r'[　-ヿ㐀-䶿一-鿿豈-﫿]')
def ph(s): return sorted(re.findall(r'%[0-9.]*[dsfuxX%]|\{[0-9]+\}',s))
def sep(s): return s.count(';#/')
bad=0
for L in ['id','tl','pl','ja']:
    gen={r['id']:r['tx'] for r in json.load(open(os.path.join(W,'out_data_gap',L,'%s_gap_p1.json'%L),encoding='utf-8'))['results']}
    for i,t in gen.items():
        en=STR[i]['en']; bud=STR[i]['budget']; errs=[]
        if ph(t)!=ph(en): errs.append('placeholder %r!=%r'%(ph(t),ph(en)))
        if sep(t)!=sep(en): errs.append('sep %d!=%d'%(sep(t),sep(en)))
        if len(t.encode('utf-8'))>bud: errs.append('budget %d>%d'%(len(t.encode('utf-8')),bud))
        if '\ufffd' in t: errs.append('fffd')
        if L!='ja' and CJK.search(t): errs.append('CJK in non-ja')
        if t.strip()=='': errs.append('empty')
        if errs:
            bad+=1
            if bad<=40: print(L,i,repr(en),'->',repr(t),errs)
    print(L,'checked',len(gen))
print('TOTAL BAD',bad)
