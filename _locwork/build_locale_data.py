# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Build a translated Data/Local/<Dir> folder for one locale.

Copies the pristine English language folder, renames files for the target
locale, and substitutes every translatable text field with the agent-produced
translations. Fields that are untranslated, malformed (placeholder/semicolon
mismatch) or too long for their fixed BMD slot stay English and are reported.

Usage: python build_locale_data.py <locale> [--write]
       e.g. python build_locale_data.py ja
"""
import json, os, re, sys, glob, shutil, struct

ROOT = config.WORKSPACE
WORK = os.path.join(ROOT, '_locwork')
sys.path.insert(0, os.path.join(ROOT, 'loc'))
import formats
from bmd import read_questwords, write_questwords

LOCALE = sys.argv[1]
WRITE = '--write' in sys.argv
state = json.load(open(os.path.join(WORK, 'state.json'), encoding='utf-8'))
DIR, SUF = state['data_dirs'][LOCALE]
SRC = os.path.join(ROOT, 'MuMain', 'src', 'bin', 'Data', 'Local', 'Eng')
OUT = os.path.join(WORK, 'build', DIR)

# --- gather translations (canonical finalized chunks only) ---
id2tx = {}
fdir = os.path.join(WORK, 'out_data', LOCALE, 'final')
for g in ['g1','g2','g3','g4','g5']:
    p = os.path.join(fdir, f'{g}.json')
    for r in json.load(open(p, encoding='utf-8'))['results']:
        tx=(r.get('tx') or '').strip()
        if tx:
            id2tx[int(r['id'])] = tx
STR = {s['id']: s for s in json.load(
    open(os.path.join(ROOT, 'loc', 'strings.json'), encoding='utf-8'))['strings']}
en2tx = {}
for sid, s in STR.items():
    t = id2tx.get(sid, '').strip()
    if t:
        en2tx[s['en']] = t

PH = re.compile(r'%\d*(?:\.\d+)?[dsfuxXl%]|\{[0-9]+\}')
def phset(t): return sorted(PH.findall(t))

def mixed_decode(bb):
    """UTF-8 text with occasional CP949 punctuation/arrows (as shipped in Eng BMD)."""
    out=[]; i=0
    while i<len(bb):
        if bb[i]<0x80: out.append(chr(bb[i])); i+=1; continue
        dec=None; used=2
        for L in (3,2):
            try:
                cand=bb[i:i+L].decode('utf-8')
                if all(ord(c)>=0x80 for c in cand): dec=cand; used=L; break
            except Exception: pass
        if dec is None:
            dec=bb[i:i+2].decode('cp949','replace'); used=2
        out.append(dec); i+=used
    return ''.join(out)

def acceptable(en, t, s):
    if not t:
        return False, 'missing'
    if phset(en) != phset(t):
        return False, 'placeholder'
    if en.count(';') != t.count(';'):
        return False, 'semicolon'
    if en.count('#') != t.count('#'):
        return False, 'hash'
    if en.count('/') != t.count('/'):
        return False, 'slash'
    if len(t.encode('utf-8')) > s['budget']:
        return False, f'budget {len(t.encode("utf-8"))}>{s["budget"]}'
    return True, ''

stats = {'fields': 0, 'kept_en': 0, 'reject': {}}
rejects = []

# a handful of NPC names absent from strings.json (brands stay English)
NPC_OVERRIDE = {
 'de': {'Summoner':'Beschwörer','Re-Initialization Helper':'Re-Initialisierungs-Helfer'},
 'id': {'Summoner':'Pemanggil','Re-Initialization Helper':'Pembantu Inisialisasi Ulang'},
 'ja': {'Summoner':'サマナー','Re-Initialization Helper':'初期化ヘルパー'},
 'pl': {'Summoner':'Przyzywacz','Re-Initialization Helper':'Pomocnik ponownej inicjalizacji'},
 'ru': {'Summoner':'Призыватель','Re-Initialization Helper':'Помощник по повторной инициализации'},
 'tl': {'Summoner':'Summoner','Re-Initialization Helper':'Re-Initialization Helper'},
 'uk': {'Summoner':'Прикликач','Re-Initialization Helper':'Помічник повторної ініціалізації'},
}.get(LOCALE, {})
def pick(en):
    """Return translation for an English source string, or '' to keep English."""
    if en in NPC_OVERRIDE:
        return NPC_OVERRIDE[en]
    t = en2tx.get(en, '')
    if not t:
        stats['kept_en'] += 1
        return ''
    # find the backing string record (any id sharing this English text)
    s = next((x for x in STR.values() if x['en'] == en), None)
    if s is None:
        return t
    ok, why = acceptable(en, t, s)
    if not ok:
        stats['reject'][why] = stats['reject'].get(why, 0) + 1
        rejects.append((why, en[:60], t[:40]))
        stats['kept_en'] += 1
        return ''
    stats['fields'] += 1
    return t

# per-kind: (kind, source file, target file)
FIXED = [
    ('item',          'item_eng.bmd',                  f'item_{SUF}.bmd'),
    ('skill',         'skill_eng.bmd',                 f'skill_{SUF}.bmd'),
    ('quest',         'Quest_eng.bmd',                 f'Quest_{SUF}.bmd'),
    ('movereq',       'MoveReq_eng.bmd',               f'MoveReq_{SUF}.bmd'),
    ('socket',        'socketitem_eng.bmd',            f'socketitem_{SUF}.bmd'),
    ('setoption',     'itemsetoption_eng.bmd',         f'itemsetoption_{SUF}.bmd'),
    ('buff',          'BuffEffect_eng.bmd',            f'BuffEffect_{SUF}.bmd'),
    ('harmony',       'JewelOfHarmonyOption_eng.bmd',  f'JewelOfHarmonyOption_{SUF}.bmd'),
    ('mastertooltip', 'MasterSkillTooltip_eng.bmd',    f'MasterSkillTooltip_{SUF}.bmd'),
]
# files without translatable kinds in strings.json: ship English fallback copies
PLAIN_COPY = ['ItemTooltip_eng.bmd', 'ItemTooltipText_eng.bmd',
              'ItemLevelTooltip_eng.bmd', 'JewelOfHarmonySmelt_eng.bmd']
PLAIN_RENAME = {p: p.replace('_eng', f'_{SUF}') for p in PLAIN_COPY}

def stage():
    if os.path.exists(OUT):
        shutil.rmtree(OUT)
    os.makedirs(OUT)
    # text-bearing fixed files
    for _, src, dst in FIXED:
        shutil.copy2(os.path.join(SRC, src), os.path.join(OUT, dst))
    # english-fallback files
    for src in PLAIN_COPY:
        shutil.copy2(os.path.join(SRC, src), os.path.join(OUT, PLAIN_RENAME[src]))
    # slide
    shutil.copy2(os.path.join(SRC, 'slide_eng.bmd'), os.path.join(OUT, f'slide_{SUF}.bmd'))
    # npc names
    shutil.copy2(os.path.join(SRC, 'NpcName_Eng.txt'), os.path.join(OUT, f'NpcName_{DIR}.txt'))
    # questwords
    shutil.copy2(os.path.join(SRC, 'QuestWords_eng.bmd'), os.path.join(OUT, f'QuestWords_{SUF}.bmd'))
    # baked map-name art keeps English image filenames
    shutil.copytree(os.path.join(SRC, 'ImgsMapName'), os.path.join(OUT, 'ImgsMapName'))
    # minimap BMDs are loaded as Minimap_<map>_<ML>.bmd -> rename _eng suffix
    os.makedirs(os.path.join(OUT, 'Minimap'))
    for mp in glob.glob(os.path.join(SRC, 'Minimap', '*')):
        name = os.path.basename(mp).replace('_eng.', f'_{SUF}.')
        shutil.copy2(mp, os.path.join(OUT, 'Minimap', name))

if not WRITE:
    print(f'locale {LOCALE} -> {DIR} (suffix {SUF})')
    print(f'translations present: {len(id2tx)}/{len(STR)} ids, {len(en2tx)} unique strings')
else:
    stage()

    # ---- fixed-record formats ----
    for kind, src, dst in FIXED:
        path = os.path.join(OUT, dst)
        f = formats.open_fmt(kind, path)
        n = 0
        for i in range(f.N):
            for nm, off, ln in formats.FMT[kind]['fields']:
                en = f.field(i, off, ln).strip()
                if not en:
                    continue
                t = pick(en)
                if t:
                    try:
                        f.set_field(i, off, ln, t); n += 1
                    except ValueError:
                        stats['kept_en'] += 1
                        stats['reject']['overflow'] = stats['reject'].get('overflow', 0) + 1
        open(path, 'wb').write(f.encode())
        print(f'  {kind:14s} {n:5d} fields')

    # ---- minimap (20 files) ----
    nmm = 0
    for p in sorted(glob.glob(os.path.join(OUT, 'Minimap', '*.bmd'))):
        f = formats.FixedRecs(p, 116, 100, key=0x2BC1, trailer=45)
        n = 0
        for i in range(100):
            en = f.field(i, 16, 100).strip()
            if en:
                t = pick(en)
                if t:
                    f.set_field(i, 16, 100, t); n += 1
        open(p, 'wb').write(f.encode())
        nmm += n
    print(f'  {"minimap":14s} {nmm:5d} fields')

    # ---- slide ----
    sp = os.path.join(OUT, f'slide_{SUF}.bmd')
    raw, buf = formats.open_slide(sp)
    n = 0
    for (L, j, iNumber, off, txt) in formats.slide_slots(buf):
        en = txt.strip()
        if not en:
            continue
        t = pick(en)
        b = t.encode('utf-8') if t else b''
        if b and len(b) < 256:
            buf[off:off+256] = b'\x00'*256; buf[off:off+len(b)] = b; n += 1
    open(sp, 'wb').write(formats.encode_slide(buf))
    print(f'  {"slide":14s} {n:5d} fields')

    # ---- questwords (variable records) ----
    qp = os.path.join(OUT, f'QuestWords_{SUF}.bmd')
    qw, sz = read_questwords(qp)
    # records the English client left in Korean (not in strings.json); translated by ID
    krgap_path = os.path.join(WORK, 'kr_gap_translations.json')
    KR_GAP = {}
    if os.path.exists(krgap_path):
        KR_GAP = {int(k): v for k, v in json.load(open(krgap_path, encoding='utf-8')).get(LOCALE, {}).items()}
    n = 0; ng = 0
    newrecs = []
    for idx, txt in qw:
        if idx in KR_GAP:
            newrecs.append((idx, KR_GAP[idx])); ng += 1; n += 1; continue
        # normalize leftover CP949 punctuation (e.g. ellipsis) to UTF-8 like Chs
        s_full = mixed_decode(txt).strip('\x00')
        s = s_full.strip()
        t = pick(s) if s else ''
        if t:
            newrecs.append((idx, t)); n += 1
        elif s_full and not any(0xAC00 <= ord(c) <= 0xD7A3 for c in s_full):
            newrecs.append((idx, s_full))
        else:
            newrecs.append((idx, txt))
    write_questwords(qp, newrecs)
    print(f'  {"questwords":14s} {n:5d}/{len(qw)} records (kr-gap {ng})')

    # ---- npcname txt (byte-level: legacy CP949 //comments must survive) ----
    npp = os.path.join(OUT, f'NpcName_{DIR}.txt')
    raw = open(npp, 'rb').read()
    line_pat = re.compile(rb'^(\d+\s+\d+\s+")([^"]*)(")')
    n = 0
    out_lines = []
    for ln in raw.splitlines(keepends=True):
        m = line_pat.match(ln)
        if not m:
            out_lines.append(ln)
            continue
        name = m.group(2).decode('utf-8')  # English source names are pure ASCII
        t = pick(name) or (pick(name.strip()) if name != name.strip() else '')
        if t:
            n += 1
            out_lines.append(m.group(1) + t.encode('utf-8') + m.group(3) + ln[m.end():])
        else:
            out_lines.append(ln)
    open(npp, 'wb').write(b''.join(out_lines))
    print(f'  {"npcname":14s} {n:5d} names')

print(f'stats: translated fields={stats["fields"]} kept_english={stats["kept_en"]} rejects={stats["reject"]}')
for r in rejects[:20]:
    print('   REJECT', r)
missing_ids = [i for i in STR if i not in id2tx]
print(f'missing translation ids: {len(missing_ids)}')
