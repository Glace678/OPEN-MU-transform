# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Proofread the deployed Chs BMD language pack via loc/formats.py."""
import sys, os, re, glob
sys.path.insert(0, config.LOC)
import opencc
import formats
from bmd import read_questwords

t2s = opencc.OpenCC('t2s')
BAD_IDIOMS = ['伺服器', '软体', '硬体', '预设', '资料', '资讯', '视讯', '网路',
              '线上', '帐号', '帐户', '滑鼠', '列印', '荧幕', '萤幕', '记忆体',
              '程式', '档案', '哥布尔', '热键', '网咖', '咖啡厅', '实作', '巡览',
              '重设', '拷贝', '全屏幕', '储存', '登入', '登出']
MOJIBAKE = ['锟斤拷', '�', 'Ã']

out = open(os.path.join(W, 'bmd_audit.txt'), 'w', encoding='utf-8')
stats = {}

def check(kind, loc, text):
    t = text.strip()
    if not t:
        return
    probs = []
    if t2s.convert(t) != t:
        probs.append('TRAD')
    for w in BAD_IDIOMS:
        if w in t:
            probs.append('IDIOM:' + w)
    for w in MOJIBAKE:
        if w in t:
            probs.append('MOJIBAKE')
    if probs:
        out.write(f"[{kind}] {loc} {probs}: {t[:150]}\n")
        stats[kind] = stats.get(kind, 0) + 1

KINDS = ['item', 'skill', 'quest', 'movereq', 'socket', 'setoption',
         'buff', 'harmony', 'mastertooltip']
for kind in KINDS:
    try:
        f = formats.open_fmt(kind)
    except Exception as e:
        out.write(f"[{kind}] OPEN-FAIL: {e}\n")
        continue
    for i in range(f.N):
        for nm, off, ln in formats.FMT[kind]['fields']:
            check(kind, f'{i}:{nm}', f.field(i, off, ln))

# quest words (NPC dialogues)
try:
    qw, _ = read_questwords(os.path.join(formats.CHS, 'QuestWords_chs.bmd'))
    for idx, txt in qw:
        try:
            s = txt.decode('utf-8')
        except UnicodeDecodeError:
            check('questwords', idx, txt.decode('utf-8', errors='replace'))
            continue
        for j, seg in enumerate(s.split(';')):
            check('questwords', f'{idx}#{j}', seg)
except Exception as e:
    out.write(f"[questwords] FAIL: {e}\n")

# minimap names
for p in sorted(glob.glob(os.path.join(formats.CHS, 'Minimap', '*.bmd'))):
    try:
        f = formats.FixedRecs(p, 116, 100, key=0x2BC1, trailer=45)
        for i in range(100):
            check('minimap', f'{os.path.basename(p)}#{i}', f.field(i, 16, 100))
    except Exception as e:
        out.write(f"[minimap] {os.path.basename(p)} FAIL: {e}\n")

# npc name txt (utf-8; comment lines may be legacy encoding)
try:
    raw = open(os.path.join(formats.CHS, 'NpcName_Chs.txt'), 'rb').read()
    for ln, line in enumerate(raw.splitlines(), 1):
        try:
            s = line.decode('utf-8')
            check('npcname', ln, s)
        except UnicodeDecodeError:
            out.write(f"[npcname] {ln} NON-UTF8 BYTES: {line[:60]!r}\n")
except Exception as e:
    out.write(f"[npcname] FAIL: {e}\n")

out.write('\nSTATS: ' + repr(stats) + '\n')
out.close()
print('done', stats)
