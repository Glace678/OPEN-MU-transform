# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Validate translated results and apply them back into the Chs BMD files.

Usage:
  python apply_translations.py            # validate only, report problems
  python apply_translations.py --write    # validate, then re-encode & deploy
"""
import json, os, re, glob, shutil, sys, struct
import formats
from bmd import read_questwords, write_questwords, bux

with open(config.STRINGS_JSON, encoding='utf-8') as _fh:
    STR = json.load(_fh)['strings']
id2en = {s['id']: s for s in STR}

# gather translated id->zh from out/*.json
id2zh = {}
outdir = config.LOC_OUT
if os.path.isdir(outdir):
    for fn in sorted(os.listdir(outdir)):
        if not fn.endswith('.json'):
            continue
        try:
            with open(os.path.join(outdir, fn), encoding='utf-8') as fh:
                d = json.load(fh)
        except Exception as e:
            print('!! cannot parse', fn, e); continue
        for r in d.get('results', []):
            id2zh[int(r['id'])] = r['zh'].strip()

WRITE = '--write' in sys.argv

# ---------- validation ----------
PH = re.compile(r'%\d*(?:\.\d+)?[dsfuxX%]|\{[0-9]+\}')
# NOTE: only genuinely TRADITIONAL-only characters go here. Shared/simplified
# chars must NOT be included or they false-positive -- e.g. 害 is identical in
# both scripts and appears in the common word 伤害 (damage); it was removed
# after flagging 171 correct strings. Audit a new char with t2s first: keep it
# only if t2s(c) != c.
TRAD = set('發動裝傷擊殺煉藥經點這裡開關場當與進遠離連續還應對戰們劍師術語義務實驗證識選項顯視頻頁碼藍綠紅黃錢銀鐵鋼雙嚮導遊戲單凍遲鈍煉獄鬥爭寧願嚮')

def _norm_en_fmt(t):
    # Upstream Webzen MasterSkillTooltip strings contain 8 entries where %0.2f%%
    # or %0.2f. lost the 'f' conversion specifier. Normalize for validation only
    # while preserving the raw `en` lookup key for BMD matching.
    s = t.replace('%0.2%%', '%0.2f%%')
    return re.sub(r'%0\.2(?=\.|$)', '%0.2f', s)

def phset(t):
    # normalise: %% counts as a literal % token
    toks = PH.findall(t)
    return sorted(toks)

problems = []
missing = []
en2zh = {}
for s in STR:
    sid, en, budget, kinds = s['id'], s['en'], s['budget'], s['kinds']
    if sid not in id2zh:
        missing.append(sid); continue
    zh = id2zh[sid]
    en2zh[en] = zh
    en_fmt = _norm_en_fmt(en)
    # placeholder parity
    if phset(en_fmt) != phset(zh):
        problems.append((sid, 'placeholder', en[:40], phset(en_fmt), phset(zh)))
    # ';' parity (dialogue)
    if ';' in en and en.count(';') != zh.count(';'):
        problems.append((sid, 'semicolon', en[:40], en.count(';'), zh.count(';')))
    # '#' parity (master tooltips)
    if '#' in en and en.count('#') != zh.count('#'):
        problems.append((sid, 'hash', en[:40], en.count('#'), zh.count('#')))
    # '/' parity for buff descriptions
    if 'buff' in kinds and en.count('/') != zh.count('/'):
        problems.append((sid, 'slash', en[:40], en.count('/'), zh.count('/')))
    # budget
    bl = len(zh.encode('utf-8'))
    if bl > budget:
        problems.append((sid, f'budget({bl}>{budget})', en[:50], '', zh[:40]))
    # residual English words (>=4 consecutive latin letters) -> soft warning
    leftover = re.findall(r'[A-Za-z]{4,}', zh)
    leftover = [w for w in leftover if w.upper() not in ('PVP','PVE','CASH','INFO')]
    if leftover and re.search(r'[A-Za-z]{4,}', en):
        # allow if en was essentially already that token (abbreviations)
        if not re.fullmatch(r'[\d\sA-Za-z/_,.:%+\-()\']{0,12}', en):
            problems.append((sid, 'maybe-english', en[:40], '', ','.join(leftover[:4])))
    # traditional chars
    tc = [c for c in zh if c in TRAD]
    if tc:
        problems.append((sid, 'traditional?', en[:30], '', ''.join(sorted(set(tc)))))

print(f'translated ids: {len(id2zh)}/{len(STR)}  missing: {len(missing)}')
print(f'problems: {len(problems)}')
from collections import Counter
pc = Counter(p[1] for p in problems)
print(' by type:', dict(pc))
for p in problems[:60]:
    print('  ', p)

if missing:
    print('MISSING IDS:', missing[:40])

# ---------- apply ----------
def backup(path):
    b = path + '.bak-en'
    if not os.path.exists(b):
        shutil.copy2(path, b)

def _atomic_write_bytes(path, data):
    tmp = path + '.tmp'
    with open(tmp, 'wb') as fh:
        fh.write(data)
    os.replace(tmp, path)

FIXED_FIELD = {k: {nm: (off, ln) for nm, off, ln in formats.FMT[k]['fields']}
               for k in ['item','skill','quest','movereq','socket','setoption','buff','harmony','mastertooltip']}

if WRITE:
    if missing:
        print('!! not writing: missing translations'); sys.exit(1)
    hard = [p for p in problems if p[1] in ('placeholder','semicolon','hash','slash') or str(p[1]).startswith('budget')]
    if hard:
        print(f'!! not writing: {len(hard)} hard problems'); sys.exit(1)

    # fixed-record formats
    for kind in ['item','skill','quest','movereq','socket','setoption','buff','harmony','mastertooltip']:
        path = formats.FILES[kind]
        f = formats.open_fmt(kind, path)
        n = 0
        for i in range(f.N):
            for nm,(off,ln) in FIXED_FIELD[kind].items():
                en = f.field(i, off, ln).strip()
                if en in en2zh:
                    f.set_field(i, off, ln, en2zh[en]); n += 1
        out = f.encode()
        backup(path)
        _atomic_write_bytes(path, out)
        print(f'  wrote {kind:14s} {n:4d} fields -> {os.path.basename(path)}')

    # minimap (20 files)
    for p in sorted(glob.glob(os.path.join(formats.CHS, 'Minimap', '*.bmd'))):
        f = formats.FixedRecs(p, 116, 100, key=0x2BC1, trailer=45)
        n=0
        for i in range(100):
            en = f.field(i,16,100).strip()
            if en in en2zh:
                f.set_field(i,16,100,en2zh[en]); n+=1
        backup(p); _atomic_write_bytes(p, f.encode())
        if n: print(f'  wrote minimap {os.path.basename(p):28s} {n}')

    # slide
    sp = os.path.join(formats.CHS, 'slide_chs.bmd')
    raw, buf = formats.open_slide(sp)
    n=0
    for (L,j,iNumber,off,txt) in formats.slide_slots(buf):
        if txt.strip() in en2zh:
            zh = en2zh[txt.strip()]
            b = zh.encode('utf-8')
            if len(b) < 256:
                buf[off:off+256]=b'\x00'*256; buf[off:off+len(b)]=b; n+=1
            else:
                print('  slide too long', L, j)
    backup(sp); _atomic_write_bytes(sp, formats.encode_slide(buf))
    print(f'  wrote slide {n} slots')

    # questwords (variable records)
    qp = os.path.join(formats.CHS, 'QuestWords_chs.bmd')
    qw, sz = read_questwords(qp)
    n=0
    newrecs=[]
    for idx, txt in qw:
        s = txt.decode('utf-8','replace').strip('\x00').strip()
        if s in en2zh:
            newrecs.append((idx, en2zh[s])); n+=1
        else:
            newrecs.append((idx, txt))
    backup(qp)
    newlen = write_questwords(qp, newrecs)
    print(f'  wrote questwords {n}/{len(qw)} records ({newlen} bytes)')

    # npcname txt (regex replace quoted names)
    npp = os.path.join(formats.CHS, 'NpcName_Chs.txt')
    with open(npp, 'rb') as fh:
        raw = fh.read()
    text = raw.decode('utf-8','replace')
    def repl(m):
        nm = m.group(2)
        return m.group(1) + en2zh.get(nm, nm) + m.group(3)
    pat = re.compile(r'(\d+\s+\d+\s+")([^"]*)(")')
    newtext, nn = pat.subn(repl, text)
    # also free-standing minimap-style names are in quotes already
    backup(npp)
    _atomic_write_bytes(npp, newtext.encode('utf-8'))
    print(f'  wrote NpcName lines replaced={nn}')

    print('DEPLOY COMPLETE')
