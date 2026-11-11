# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Split strings.json into chunk files for parallel translation agents.

Pass --clean-out to also wipe loc/out/*.json (preserved by default so regenerating
chunks from an updated strings.json never destroys completed translations).
"""
import json, os, sys

with open(config.STRINGS_JSON, encoding='utf-8') as fh:
    data = json.load(fh)['strings']
for entry in data:
    if '\ufffd' in entry.get('en', ''):
        raise ValueError(f"U+FFFD replacement char found in strings.json id={entry.get('id')}")

outdir = config.LOC_CHUNKS
os.makedirs(outdir, exist_ok=True)
for f in os.listdir(outdir):
    p = os.path.join(outdir, f)
    if f.endswith('.json') and os.path.isfile(p):
        os.remove(p)

os.makedirs(config.LOC_OUT, exist_ok=True)
if '--clean-out' in sys.argv[1:]:
    for f in os.listdir(config.LOC_OUT):
        p = os.path.join(config.LOC_OUT, f)
        if f.endswith('.json') and os.path.isfile(p):
            os.remove(p)

def tone(kinds, budget):
    if 'questwords' in kinds:
        return 'dialogue'      # NPC 对话，自然口语，保留 ;
    if budget <= 31:
        return 'name'          # 短名称（道具/技能/地图/NPC），4-8字精简
    if 'mastertooltip' in kinds or 'buff' in kinds or 'socket' in kinds or 'harmony' in kinds or 'setoption' in kinds:
        return 'option'        # 属性说明模板，保留占位符与 # /
    return 'text'              # 帮助/说明文本

dialogue = [s for s in data if tone(s['kinds'], s['budget']) == 'dialogue']
names    = [s for s in data if tone(s['kinds'], s['budget']) == 'name']
options  = [s for s in data if tone(s['kinds'], s['budget']) == 'option']
texts    = [s for s in data if tone(s['kinds'], s['budget']) == 'text']

def chunk(lst, n):
    for i in range(0, len(lst), n):
        yield lst[i:i+n]

plan = []
def emit(group, lst, size):
    for ci, c in enumerate(chunk(lst, size)):
        fn = f'{group}_{ci:02d}.json'
        target = os.path.join(outdir, fn)
        tmp = target + '.tmp'
        with open(tmp, 'w', encoding='utf-8') as fh:
            json.dump({'group': group, 'items': c}, fh, ensure_ascii=False, indent=1)
        os.replace(tmp, target)
        plan.append((fn, len(c)))

emit('dialogue', dialogue, 50)
emit('name', names, 130)
emit('option', options, 120)
emit('text', texts, 120)

print(f'dialogue {len(dialogue)}  names {len(names)}  options {len(options)}  texts {len(texts)}')
print(f'total chunks: {len(plan)}')
for fn, n in plan:
    print(f'  {fn}  {n}')
