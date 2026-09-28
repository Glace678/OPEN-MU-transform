# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Export all translatable English strings -> strings.json (deduped, with byte budget).
Also writes locations.json mapping en->list of locations for re-applying.
Special: questwords (';'-separated dialogue), npcname (handled at apply time)."""
import json, glob, os, struct, re
import formats
from bmd import read_questwords

uniq = {}          # en -> {'budget':int,'kinds':set}
locs = {}          # en -> list of location tuples (kind, a, b, field)

def add(kind, a, b, field, text, budget):
    t = text.strip()
    if not t:
        return
    # only translate strings containing at least one ASCII letter (skip pure numbers/symbols)
    if not re.search(r'[A-Za-z]', t):
        return
    if t not in uniq:
        uniq[t] = {'budget': budget, 'kinds': set()}
    if budget < uniq[t]['budget']:
        uniq[t]['budget'] = budget
    uniq[t]['kinds'].add(kind)
    locs.setdefault(t, []).append([kind, a, b, field])

FIXED = ['item','skill','quest','movereq','socket','setoption','buff','harmony','mastertooltip']

for kind in FIXED:
    f = formats.open_fmt(kind)
    for i in range(f.N):
        for nm, off, ln in formats.FMT[kind]['fields']:
            t = f.field(i, off, ln)
            add(kind, i, None, nm, t, ln-1)

# minimap (20 files)
for p in sorted(glob.glob(formats.CHS + r'\Minimap\*.bmd')):
    name = os.path.basename(p)
    f = formats.FixedRecs(p, 116, 100, key=0x2BC1, trailer=45)
    for i in range(100):
        t = f.field(i, 16, 100)
        add('minimap', name, i, 'name', t, 99)

# slide
raw, buf = formats.open_slide()
for (L, j, iNumber, off, txt) in formats.slide_slots(buf):
    add('slide', L, j, 'slot', txt, 255)

# questwords (variable, whole-record dialogue; segments ';')
qw, sz = read_questwords(formats.CHS + r'\QuestWords_chs.bmd')
for idx, txt in qw:
    s = txt.decode('utf-8', 'replace').strip('\x00').strip()
    if s and re.search(r'[A-Za-z一-鿿]', s):
        add('questwords', idx, None, 'dialog', s, 4096)

# npcname txt -> unique quoted names
npc = {}
npc_path = formats.CHS + r'\NpcName_Chs.txt'
for line in open(npc_path, 'rb').read().decode('utf-8', 'replace').splitlines():
    m = re.search(r'^\s*(\d+)\s+\d+\s+"([^"]*)"', line)
    if m and m.group(2).strip() and re.search(r'[A-Za-z]', m.group(2)):
        nm = m.group(2)
        npc[nm] = min(npc.get(nm, 99), 99)
for nm in npc:
    add('npcname', nm, None, 'monster', nm, 99)

# emit
items = [{'id': k, 'en': en, 'budget': uniq[en]['budget'],
          'kinds': sorted(uniq[en]['kinds'])} for k, en in
         enumerate(sorted(uniq.keys()))]
with open(config.STRINGS_JSON, 'w', encoding='utf-8') as fh:
    json.dump({'strings': items}, fh, ensure_ascii=False, indent=0)
# locs for the apply step
with open(os.path.join(config.LOC, 'locs.json'), 'w', encoding='utf-8') as fh:
    json.dump({en: v for en, v in locs.items()}, fh, ensure_ascii=False)

# per-kind counts
from collections import Counter
c = Counter()
for en, l in locs.items():
    for loc in l:
        c[loc[0]] += 1
print('total unique strings:', len(uniq))
print('total locations:', sum(len(v) for v in locs.values()))
for k, v in sorted(c.items(), key=lambda x: -x[1]):
    print(f'  {k:14s} {v}')
tight = sum(1 for en in uniq if uniq[en]['budget'] <= 31)
print('strings with tight budget (<=31 bytes, names):', tight)
