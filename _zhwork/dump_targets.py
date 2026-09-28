# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import re, os
LOC = config.LOCALIZATION
out = open(os.path.join(W, 'zh_targets.txt'), 'w', encoding='utf-8')

def dump(base, needles):
    zh = open(os.path.join(LOC, base + '.zh-CN.resx'), encoding='utf-8').read()
    en = open(os.path.join(LOC, base + '.en.resx'), encoding='utf-8').read()
    ent = dict((m.group(1), m.group(2))
               for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', en, re.S))
    for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', zh, re.S):
        name, v = m.group(1), m.group(2)
        if any(n in v for n in needles):
            out.write(f"[{base}] {name}\n  EN: {ent.get(name,'?')}\n  ZH: {v}\n\n")

dump('Game', ['哥布尔', '黑客', '热键', 'PC咖啡厅', '100%中奖', '啊!', '迪诺兰特'])
dump('Game', ['警告'])
dump('Editor', []) if False else None
# Editor all values containing ASCII ! or : after CJK
zh = open(os.path.join(LOC, 'Editor.zh-CN.resx'), encoding='utf-8').read()
for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', zh, re.S):
    name, v = m.group(1), m.group(2)
    if re.search(r'[一-鿿]!|[一-鿿]:|[一-鿿],|[一-鿿]\.\s*$', v):
        out.write(f"[Editor-PUNCT] {name}\n  ZH: {v}\n\n")
# all exclamation-space patterns in Game/Dialog
for base in ['Game', 'Dialog']:
    zh = open(os.path.join(LOC, base + '.zh-CN.resx'), encoding='utf-8').read()
    for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', zh, re.S):
        name, v = m.group(1), m.group(2)
        if re.search(r'[！!]\s+[！!]|!\s*[一-鿿]', v):
            out.write(f"[{base}-BANG] {name}\n  ZH: {v}\n\n")
out.close()
print('ok')
