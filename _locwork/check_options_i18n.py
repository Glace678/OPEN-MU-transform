# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Verify every Options-window I18N id is localized in all 12 UIs.

resx keys are the English source strings; the generated Game.h maps each C++
identifier to that string via a trailing comment:
    extern const wchar_t* SoundVolume;   // Sound volume
"""
import os, re, xml.etree.ElementTree as ET

ROOT = config.MU_MAIN_SRC
GENH = os.path.join(config.GENERATED_I18N, 'Game.h')
LOCDIR = os.path.join(ROOT, 'Localization')
LOCALES = ['en','de','es','id','ja','pl','pt','ru','tl','uk','zh-CN','zh-TW']

ids = [l.strip() for l in open(os.path.join(W,'opt_keys.txt'), encoding='utf-8') if l.strip()]

id2en = {}
decl = re.compile(r'extern const wchar_t\*\s+(\w+)\s*;\s*//\s*(.*)$')
for line in open(GENH, encoding='utf-8'):
    m = decl.search(line)
    if m:
        id2en[m.group(1)] = m.group(2).strip()

def load(loc):
    root = ET.parse(os.path.join(LOCDIR, f'Game.{loc}.resx')).getroot()
    d = {}
    for dn in root.iter('data'):
        v = dn.find('value')
        d[dn.get('name')] = (v.text if v is not None and v.text else '') or ''
    return d

tables = {loc: load(loc) for loc in LOCALES}
problems = 0
shown = 0
for i in ids:
    en_key = id2en.get(i)
    if en_key is None:
        print(f'NO IDENTIFIER IN GENERATED HEADER: {i}')
        problems += 1
        continue
    row = []
    bad = []
    for loc in LOCALES:
        if en_key not in tables[loc]:
            row.append('MISSING'); bad.append(loc)
        elif not tables[loc][en_key].strip():
            row.append('EMPTY'); bad.append(loc)
        elif loc != 'en' and tables[loc][en_key].strip() == tables['en'].get(en_key,'').strip():
            row.append('=en'); bad.append(loc)
        else:
            row.append('ok')
    if bad:
        shown += 1
        problems += len(bad)
        print(f'{i:32s} [{en_key[:40]:40s}]', ' '.join(f'{l}:{s}' for l,s in zip(LOCALES,row) if s!='ok'))

print(f'\n{len(ids)} option ids across {len(LOCALES)} locales; {problems} problem cells')

print('\n--- Japanese values for visible Options labels ---')
for i in ids:
    k = id2en.get(i)
    if k and i in {'Language','Font','Resolution','WindowedMode','SoundVolume','MusicVolume',
                   'AutomaticAttack','BeepSoundForWhispering','SlideHelp','EffectLimitation',
                   'RenderFullEffects','VerticalSync','FrameRate','Gamepad','GamepadEnabled',
                   'PointerSpeed','Haptics','RestartRequiredTitle','RestartLanguageMessage'}:
        print(f'{i:26s} = {tables["ja"].get(k, "<<MISSING>>")}')
