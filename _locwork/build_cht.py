# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Build Data/Local/Cht (Traditional Chinese, Taiwan) from the Chs folder.

Every text field is converted with OpenCC s2twp (simplified -> traditional
with Taiwan phrase standard); BMD containers/checksums are rewritten through
the same machinery as the Chinese pipeline.

Usage: python build_cht.py [--write]
"""
import os, re, sys, glob, shutil

ROOT = config.WORKSPACE
WORK = os.path.join(ROOT, '_locwork')
sys.path.insert(0, os.path.join(ROOT, 'loc'))
import formats
from bmd import read_questwords, write_questwords
from opencc import OpenCC

WRITE = '--write' in sys.argv
cc = OpenCC('s2twp')
def t(s):
    return cc.convert(s)

SRC = os.path.join(ROOT, 'MuMain', 'src', 'bin', 'Data', 'Local', 'Chs')
OUT = os.path.join(WORK, 'build', 'Cht')
DIR, SUF = 'Cht', 'cht'

# kind -> (source file in Chs, target file in Cht)
FIXED = [
    ('item',          'item_chs.bmd',                  f'item_{SUF}.bmd'),
    ('skill',         'skill_chs.bmd',                 f'skill_{SUF}.bmd'),
    ('quest',         'Quest_chs.bmd',                 f'Quest_{SUF}.bmd'),
    ('movereq',       'MoveReq_chs.bmd',               f'MoveReq_{SUF}.bmd'),
    ('socket',        'socketitem_chs.bmd',            f'socketitem_{SUF}.bmd'),
    ('setoption',     'itemsetoption_chs.bmd',         f'itemsetoption_{SUF}.bmd'),
    ('buff',          'BuffEffect_chs.bmd',            f'BuffEffect_{SUF}.bmd'),
    ('harmony',       'JewelOfHarmonyOption_chs.bmd',  f'JewelOfHarmonyOption_{SUF}.bmd'),
    ('mastertooltip', 'MasterSkillTooltip_chs.bmd',    f'MasterSkillTooltip_{SUF}.bmd'),
]
PLAIN = ['ItemTooltip_chs.bmd', 'ItemTooltipText_chs.bmd',
         'ItemLevelTooltip_chs.bmd', 'JewelOfHarmonySmelt_chs.bmd']

if WRITE:
    if os.path.exists(OUT):
        shutil.rmtree(OUT)
    os.makedirs(OUT)
    for _, src, dst in FIXED:
        shutil.copy2(os.path.join(SRC, src), os.path.join(OUT, dst))
    for src in PLAIN:
        p = os.path.join(SRC, src)
        if os.path.exists(p):
            shutil.copy2(p, os.path.join(OUT, src.replace('_chs', f'_{SUF}')))
    shutil.copy2(os.path.join(SRC, 'slide_chs.bmd'), os.path.join(OUT, f'slide_{SUF}.bmd'))
    shutil.copy2(os.path.join(SRC, 'NpcName_Chs.txt'), os.path.join(OUT, f'NpcName_{DIR}.txt'))
    shutil.copy2(os.path.join(SRC, 'QuestWords_chs.bmd'), os.path.join(OUT, f'QuestWords_{SUF}.bmd'))
    shutil.copytree(os.path.join(SRC, 'ImgsMapName'), os.path.join(OUT, 'ImgsMapName'))
    # Minimap BMDs are loaded as Minimap_<map>_<ML>.bmd -> rename _chs suffix to _cht
    os.makedirs(os.path.join(OUT, 'Minimap'))
    for mp in glob.glob(os.path.join(SRC, 'Minimap', '*')):
        name = os.path.basename(mp).replace('_chs.', f'_{SUF}.')
        shutil.copy2(mp, os.path.join(OUT, 'Minimap', name))

    stat = {'fields': 0, 'skip': 0}
    skipped = []
    def conv_field(f, i, off, ln, kind):
        s = f.field(i, off, ln)
        if not s.strip():
            return
        stripped = s.strip()
        nt = t(stripped)
        # re-pad like the original (leading/trailing spaces are cosmetic)
        lead = s[:len(s)-len(s.lstrip())]
        trail = s[len(s.rstrip()):]
        b = (lead + nt + trail).encode('utf-8')
        if len(b) < ln:
            f.set_field(i, off, ln, lead + nt + trail); stat['fields'] += 1
        else:
            b2 = nt.encode('utf-8')
            if len(b2) < ln:
                f.set_field(i, off, ln, nt); stat['fields'] += 1
            else:
                stat['skip'] += 1
                if len(skipped) < 20:
                    skipped.append((kind, i, stripped[:30], nt[:30], len(b2), ln))

    for kind, src, dst in FIXED:
        path = os.path.join(OUT, dst)
        f = formats.open_fmt(kind, path)
        for i in range(f.N):
            for nm, off, ln in formats.FMT[kind]['fields']:
                conv_field(f, i, off, ln, kind)
        open(path, 'wb').write(f.encode())
        print(f'  {kind:14s} -> {dst}')
    print(f'  fixed skipped (kept simplified): {stat["skip"]}')
    for sk in skipped:
        print('    SKIP', sk)

    nmm = 0
    for p in sorted(glob.glob(os.path.join(OUT, 'Minimap', '*.bmd'))):
        f = formats.FixedRecs(p, 116, 100, key=0x2BC1, trailer=45)
        for i in range(100):
            s = f.field(i, 16, 100)
            if s.strip():
                nt = t(s.strip())
                if len(nt.encode('utf-8')) < 100:
                    f.set_field(i, 16, 100, nt); nmm += 1
        open(p, 'wb').write(f.encode())
    print(f'  minimap        {nmm}')

    sp = os.path.join(OUT, f'slide_{SUF}.bmd')
    raw, buf = formats.open_slide(sp)
    ns = 0
    for (L, j, iNumber, off, txt) in formats.slide_slots(buf):
        if txt.strip():
            b = t(txt.strip()).encode('utf-8')
            if len(b) < 256:
                buf[off:off+256] = b'\x00'*256; buf[off:off+len(b)] = b; ns += 1
    open(sp, 'wb').write(formats.encode_slide(buf))
    print(f'  slide          {ns}')

    qp = os.path.join(OUT, f'QuestWords_{SUF}.bmd')
    qw, _ = read_questwords(qp)
    newrecs = []
    for idx, txt in qw:
        s = txt.decode('utf-8', 'replace')
        nul = '\x00' if s.endswith('\x00') else ''
        body = s.rstrip('\x00').strip()
        newrecs.append((idx, t(body) + nul) if body else (idx, txt))
    write_questwords(qp, newrecs)
    print(f'  questwords     {len(qw)} records')

    # byte-level: legacy CP949 //comments survive untouched
    npp = os.path.join(OUT, f'NpcName_{DIR}.txt')
    raw = open(npp, 'rb').read()
    line_pat = re.compile(rb'^(\d+\s+\d+\s+")([^"]*)(")')
    nn = 0
    out_lines = []
    for ln in raw.splitlines(keepends=True):
        m = line_pat.match(ln)
        if not m or not m.group(2).strip():
            out_lines.append(ln)
            continue
        name = m.group(2).decode('utf-8')
        nt = t(name)
        nn += 1
        out_lines.append(m.group(1) + nt.encode('utf-8') + m.group(3) + ln[m.end():])
    open(npp, 'wb').write(b''.join(out_lines))
    print(f'  npcname        {nn} names')
    print(f'Cht build complete at {OUT} (fixed fields {stat["fields"]})')
else:
    print('dry run; pass --write to build')
