# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Validate translated resx JSON and merge it into the Game/Dialog resx files.

Usage: python merge_resx.py          # validate + report
       python merge_resx.py --write  # append/create resx entries
"""
import json, os, re, sys, glob
from xml.sax.saxutils import escape

ROOT = config.WORKSPACE
LOC = os.path.join(ROOT, 'MuMain', 'src', 'Localization')
WORK = os.path.join(ROOT, '_locwork')
WRITE = '--write' in sys.argv

PH = re.compile(r'%\d*(?:\.\d+)?[dsfuxXl%]|\{[0-9]+\}')

def phset(t):
    return sorted(PH.findall(t))

RESX_HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''

def render_entry(key, val, comment):
    s = f'  <data name="{escape(key, {chr(34): "&quot;"})}" xml:space="preserve">\n'
    s += f'    <value>{escape(val)}</value>\n'
    if comment:
        s += f'    <comment>{escape(comment)}</comment>\n'
    s += '  </data>\n'
    return s

total_problems = 0
for inp in sorted(glob.glob(os.path.join(WORK, 'in_resx', '*.json'))):
    loc = os.path.splitext(os.path.basename(inp))[0]
    spec = json.load(open(inp, encoding='utf-8'))
    outp = os.path.join(WORK, 'out_resx', f'{loc}.json')
    if not os.path.exists(outp):
        print(f'[{loc}] !! output missing'); total_problems += 1; continue
    tx = {r['key']: r.get('tx', '') for r in json.load(open(outp, encoding='utf-8'))['results']}

    by_group = {'Game': [], 'Dialog': []}
    problems = []
    for item in spec['items']:
        key, en, group = item['key'], item['en'], item['group']
        if key not in tx:
            problems.append(('missing', key[:50])); continue
        t = tx[key]
        if t is None or t == '':
            problems.append(('empty', key[:50])); continue
        if phset(en) != phset(t):
            problems.append(('placeholder', key[:42], phset(en), phset(t)))
        if group == 'Dialog' and en.count(';') != t.count(';'):
            problems.append(('semicolon', key[:42], en.count(';'), t.count(';')))
        if en.count('\n') != t.count('\n'):
            problems.append(('newline', key[:42]))
        if t == en:
            continue  # identical -> leave to built-in English fallback
        by_group[group].append((key, t, item.get('comment')))

    hard = [p for p in problems if p[0] in ('missing', 'empty', 'placeholder', 'semicolon', 'newline')]
    total_problems += len(hard)
    print(f'[{loc}] input={len(spec["items"])} toAdd Game={len(by_group["Game"])} Dialog={len(by_group["Dialog"])} problems={len(hard)}')
    for p in problems[:15]:
        print('   ', p)

    if WRITE and not hard:
        for group, new_items in by_group.items():
            if not new_items:
                continue
            path = os.path.join(LOC, f'{group}.{loc}.resx')
            if os.path.exists(path):
                text = open(path, encoding='utf-8').read()
                assert text.rstrip().endswith('</root>'), path
                block = ''.join(render_entry(k, v, c) for k, v, c in new_items)
                text = text.replace('</root>', block + '</root>')
                open(path, 'w', encoding='utf-8', newline='').write(text)
            else:
                assert group == 'Dialog', f'unexpected new file {path}'
                body = ''.join(render_entry(k, v, c) for k, v, c in new_items)
                open(path, 'w', encoding='utf-8', newline='').write(RESX_HEADER + body + '</root>\n')
        print(f'   merged -> resx')

print('TOTAL HARD PROBLEMS:', total_problems)
sys.exit(1 if total_problems and WRITE else 0)
