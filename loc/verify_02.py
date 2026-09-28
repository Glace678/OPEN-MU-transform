# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
import json, re, sys

with open(os.path.join(config.LOC_CHUNKS, 'option_02.json'), encoding='utf-8') as f:
    src = json.load(f)
with open(os.path.join(config.LOC_OUT, 'option_02.json'), encoding='utf-8') as f:
    out = json.load(f)

items = {it['id']: it for it in src['items']}
results = out['results']

problems = []

# count check
if len(results) != len(src['items']):
    problems.append(f"COUNT mismatch: out={len(results)} src={len(src['items'])}")

src_ids = [it['id'] for it in src['items']]
out_ids = [r['id'] for r in results]
if sorted(src_ids) != sorted(out_ids):
    problems.append(f"ID set mismatch. missing={set(src_ids)-set(out_ids)} extra={set(out_ids)-set(src_ids)}")
if len(set(out_ids)) != len(out_ids):
    problems.append("duplicate ids in output")

ph_re = re.compile(r'%(?:\d+\.\d+|%\d|[sdufx%])|%[sdufx%]|%%?|\{[0-9]+\}')
def phs(s):
    # extract placeholders in order: %0.2f, %0.2f%%, %d, %s, %u, %x, %%, {0}
    return re.findall(r'%0?\.?\d*[sdufx]|%%|\{[0-9]+\}', s)

# traditional-only forms (distinct from simplified); never appear in simplified text
trad_chars = set('們個這來說時對發為會學過還點線後現體書買車馬電長東門問聲開間關戰場風雲寶劍聖獸龍鳳護衛術師騎槍煉獄憤靈詛禦鬥擊殺敵陣閃凍結傷減機獲裝備藥捲軸鑰環項鏈鍛鐵漢納盧絲蓋亞樓羅麗貫雙鋼')

for r in results:
    i = items.get(r['id'])
    if not i:
        continue
    en, zh, budget = i['en'], r['zh'], i['budget']
    b = len(zh.encode('utf-8'))
    if b >= budget:
        problems.append(f"{r['id']}: BYTES {b} >= budget {budget} | {zh}")
    if phs(en) != phs(zh):
        problems.append(f"{r['id']}: PLACEHOLDER en={phs(en)} zh={phs(zh)}")
    if en.count('#') != zh.count('#'):
        problems.append(f"{r['id']}: HASH en={en.count('#')} zh={zh.count('#')}")
    if en.count('/') != zh.count('/'):
        problems.append(f"{r['id']}: SLASH en={en.count('/')} zh={zh.count('/')}")
    bad = [c for c in zh if c in trad_chars]
    if bad:
        problems.append(f"{r['id']}: TRADITIONAL? {set(bad)} in {zh}")

print("items:", len(results))
if problems:
    print("PROBLEMS:")
    for p in problems:
        print(" -", p)
    sys.exit(1)
print("ALL CHECKS PASSED")
