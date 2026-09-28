# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Placeholder/mojibake audit: zh-CN resx must keep same %s/%d/{0}/newlines as en."""
import re, os

LOC = config.LOCALIZATION
SPEC = re.compile(r'%(?:%|[-+ #0]*\d*(?:\.\d+)?[sdifgxouc])|\{[0-9]+\}|\\n')

out = open(os.path.join(W, 'zh_placeholder_audit.txt'), 'w', encoding='utf-8')
bad = 0
for base in ['Game', 'Dialog', 'Editor', 'Metadata']:
    en = open(os.path.join(LOC, base + '.en.resx'), encoding='utf-8').read()
    zh = open(os.path.join(LOC, base + '.zh-CN.resx'), encoding='utf-8').read()
    ent = dict((m.group(1), m.group(2))
               for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', en, re.S))
    for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', zh, re.S):
        name, zv = m.group(1), m.group(2)
        ev = ent.get(name)
        if ev is None:
            continue
        es = sorted(SPEC.findall(ev))
        zs = sorted(SPEC.findall(zv))
        problems = []
        if es != zs:
            problems.append('PLACEHOLDER')
        if '�' in zv or '锟斤拷' in zv or 'Ã' in zv:
            problems.append('MOJIBAKE')
        if problems:
            bad += 1
            out.write(f"[{base}] {name} {problems}\n")
            out.write(f"  EN {es}: {ev[:150]}\n")
            out.write(f"  ZH {zs}: {zv[:150]}\n\n")
out.close()
print("mismatches:", bad)
