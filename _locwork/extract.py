# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Prepare translation inputs for the full 12-locale localization pass.

Outputs:
  in_resx/<loc>.json   missing Game/Dialog resx entries for a UI locale
  in_data/<lang>/gN.json  BMD string chunks for a data language
  state.json           locale/dir mapping and chunk manifest
"""
import json, os, re
import xml.etree.ElementTree as ET

ROOT = config.WORKSPACE
LOC = os.path.join(ROOT, 'MuMain', 'src', 'Localization')
WORK = os.path.join(ROOT, '_locwork')
os.makedirs(os.path.join(WORK, 'in_resx'), exist_ok=True)
os.makedirs(os.path.join(WORK, 'in_data'), exist_ok=True)
os.makedirs(os.path.join(WORK, 'out_resx'), exist_ok=True)
os.makedirs(os.path.join(WORK, 'out_data'), exist_ok=True)

# UI locale -> BMD data dir / suffix
DATA_DIRS = {
    'en':    ('Eng', 'eng'),
    'de':    ('Ger', 'ger'),
    'es':    ('Spn', 'spn'),
    'id':    ('Ind', 'ind'),
    'ja':    ('Jpn', 'jpn'),
    'pl':    ('Pol', 'pol'),
    'pt':    ('Por', 'por'),
    'ru':    ('Rus', 'rus'),
    'tl':    ('Tgl', 'tgl'),
    'uk':    ('Ukr', 'ukr'),
    'zh-CN': ('Chs', 'chs'),
    'zh-TW': ('Cht', 'cht'),
}
# data languages that need machine translation (zh-TW via OpenCC, en/es/pt native)
MT_DATA_LANGS = ['de', 'id', 'ja', 'pl', 'ru', 'tl', 'uk']

LANG_NAMES = {
    'en': 'English', 'de': 'German', 'es': 'Spanish', 'id': 'Indonesian (Bahasa Indonesia)',
    'ja': 'Japanese', 'pl': 'Polish', 'pt': 'Brazilian Portuguese', 'ru': 'Russian',
    'tl': 'Tagalog/Filipino', 'uk': 'Ukrainian', 'zh-CN': 'Simplified Chinese',
    'zh-TW': 'Traditional Chinese (Taiwan)',
}

# ---------- 1. new restart prompt keys in Game.en.resx ----------
NEW_KEYS = [
    ('RestartRequiredTitle', 'Language changed'),
    ('RestartLanguageMessage',
     'The game content language (NPC names, quests, items and skills) only changes after restarting the game. Restart now?'),
]

def resx_path(group, loc):
    return os.path.join(LOC, f'{group}.{loc}.resx')

def parse_resx(path):
    """Return ordered list of (key, value, comment-or-None)."""
    tree = ET.parse(path)
    root = tree.getroot()
    out = []
    for d in root.findall('data'):
        key = d.get('name')
        if key is None or key.startswith('>>'):
            continue
        v = d.find('value')
        val = v.text if v is not None and v.text is not None else ''
        c = d.find('comment')
        comment = c.text if c is not None and c.text is not None else None
        out.append((key, val, comment))
    return out

def inject_new_en_keys():
    p = resx_path('Game', 'en')
    text = open(p, encoding='utf-8').read()
    for key, val in NEW_KEYS:
        if f'name="{key}"' in text:
            continue
        block = (f'  <data name="{key}" xml:space="preserve">\n'
                 f'    <value>{val}</value>\n'
                 f'  </data>\n')
        if not text.endswith('\n'):
            text += '\n'
        text = text.replace('</root>', block + '</root>')
    open(p, 'w', encoding='utf-8', newline='').write(text)

inject_new_en_keys()

# ---------- 2. missing resx entries per locale ----------
en_game = parse_resx(resx_path('Game', 'en'))
en_dialog = parse_resx(resx_path('Dialog', 'en'))
en_game_keys = {k: (v, c) for k, v, c in en_game}
en_dialog_keys = {k: (v, c) for k, v, c in en_dialog}

RESX_LOCALES = ['de', 'es', 'id', 'ja', 'pl', 'pt', 'ru', 'tl', 'uk', 'zh-TW']
DIALOG_MISSING_LOCALES = ['id', 'ja', 'ru', 'tl', 'uk', 'zh-TW']
resx_manifest = {}

for loc in RESX_LOCALES:
    items = []
    have = set()
    gp = resx_path('Game', loc)
    if os.path.exists(gp):
        have = {k for k, _, _ in parse_resx(gp)}
    for k, v, c in en_game:
        if k not in have:
            items.append({'group': 'Game', 'key': k, 'en': v,
                          'comment': c})
    if loc in DIALOG_MISSING_LOCALES:
        for k, v, c in en_dialog:
            items.append({'group': 'Dialog', 'key': k, 'en': v,
                          'comment': c})
    with open(os.path.join(WORK, 'in_resx', f'{loc}.json'), 'w', encoding='utf-8') as f:
        json.dump({'locale': loc, 'language': LANG_NAMES[loc], 'items': items},
                  f, ensure_ascii=False, indent=1)
    resx_manifest[loc] = len(items)
    print(f'resx {loc}: {len(items)} missing')

# ---------- 3. BMD data chunks ----------
STR = json.load(open(os.path.join(ROOT, 'loc', 'strings.json'), encoding='utf-8'))['strings']
by_kind = {}
for s in STR:
    for k in s['kinds']:
        by_kind.setdefault(k, []).append(s)

def chunk(items, n):
    return [items[i:i + n] for i in range(0, len(items), n)]

# Some strings carry multiple kinds; assign each unique id to exactly one
# chunk by kind priority so every string is translated once.
GROUPS = {'g1': [], 'g2': [], 'g3': [], 'g4': [], 'g5': []}
assigned = set()
def take(kind, target):
    for s in by_kind.get(kind, []):
        if s['id'] not in assigned:
            assigned.add(s['id'])
            target.append(s)
qw_taken = []
take('questwords', qw_taken)
GROUPS['g1'] = qw_taken[:len(qw_taken)//2 + len(qw_taken)%2]
GROUPS['g2'] = qw_taken[len(GROUPS['g1']):]
for k in ('item', 'npcname'):
    take(k, GROUPS['g3'])
for k in ('mastertooltip', 'buff'):
    take(k, GROUPS['g4'])
for k in ('skill', 'slide', 'minimap', 'setoption', 'movereq', 'socket', 'harmony', 'quest'):
    take(k, GROUPS['g5'])
assert sum(len(v) for v in GROUPS.values()) == len(STR), (sum(len(v) for v in GROUPS.values()), len(STR))

data_manifest = {}
for lang in MT_DATA_LANGS:
    d = os.path.join(WORK, 'in_data', lang)
    os.makedirs(d, exist_ok=True)
    od = os.path.join(WORK, 'out_data', lang)
    os.makedirs(od, exist_ok=True)
    data_manifest[lang] = {'language': LANG_NAMES[lang], 'dir': DATA_DIRS[lang][0],
                           'chunks': {}}
    for gname, items in GROUPS.items():
        payload = [{'id': s['id'], 'en': s['en'], 'budget': s['budget'],
                    'kinds': s['kinds']} for s in items]
        with open(os.path.join(d, f'{gname}.json'), 'w', encoding='utf-8') as f:
            json.dump({'locale': lang, 'language': LANG_NAMES[lang],
                       'chunk': gname, 'strings': payload},
                      f, ensure_ascii=False, indent=1)
        data_manifest[lang]['chunks'][gname] = len(items)

json.dump({'data_dirs': DATA_DIRS, 'mt_data_langs': MT_DATA_LANGS,
           'lang_names': LANG_NAMES, 'resx_missing': resx_manifest,
           'data_chunks': data_manifest},
          open(os.path.join(WORK, 'state.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)
print('data chunks:', {g: len(v) for g, v in GROUPS.items()})
print('total strings:', len(STR))
