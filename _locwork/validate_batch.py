# -*- coding: utf-8 -*-
"""Validate one translated batch output against its todo input.
Usage: python validate_batch.py <input.json> <output.json>
"""
import json, re, sys, os

PH = re.compile(r'%\d*(?:\.\d+)?[dsfuxXl%]|\{[0-9]+\}')
def phset(t): return sorted(PH.findall(t))

# forbidden for non-Japanese: CJK ideographs, Hangul, replacement char
def script_ok(lang, t):
    if '�' in t: return False
    for c in t:
        o=ord(c)
        if lang!='ja' and (0x4E00<=o<=0x9FFF or 0xAC00<=o<=0xD7A3 or 0x3040<=o<=0x30FF):
            return False
    return True

def main():
    inp=json.load(open(sys.argv[1],encoding='utf-8'))
    outp=sys.argv[2]; lang=inp['locale']
    if not os.path.exists(outp): print('MISSING OUTPUT',outp); sys.exit(2)
    out=json.load(open(outp,encoding='utf-8'))
    res={int(r['id']):str(r.get('tx','')) for r in out['results']}
    problems=[]
    for s in inp['strings']:
        i,en,bud=s['id'],s['en'],s['budget']
        if i not in res: problems.append((i,'missing','')); continue
        t=res[i]
        if not t.strip(): problems.append((i,'empty','')); continue
        if phset(en)!=phset(t): problems.append((i,f'placeholder {phset(en)} vs {phset(t)}',t[:40]))
        for ch,name in [(';','semicolon'),('#','hash'),('/','slash')]:
            if en.count(ch)!=t.count(ch): problems.append((i,f'{name} {en.count(ch)} vs {t.count(ch)}',t[:40]))
        n=len(t.encode('utf-8'))
        if n>bud: problems.append((i,f'budget {n}>{bud}',t[:40]))
        if not script_ok(lang,t): problems.append((i,'wrong-script',t[:40]))
    extra=set(res)-{s['id'] for s in inp['strings']}
    if extra: problems.append(('EXTRA',str(sorted(extra)[:10]),''))
    if problems:
        print(f'{os.path.basename(sys.argv[1])}: {len(problems)} problems / {len(inp["strings"])}')
        for p in problems[:20]: print('  ',p)
        sys.exit(1)
    print(f'{os.path.basename(sys.argv[1])}: OK {len(inp["strings"])} strings')

if __name__=='__main__': main()
