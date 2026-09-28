# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
import re
p = os.path.join(config.GENERATED_I18N, 'Game.cpp')
t = open(p, encoding='ascii', errors='replace').read()
dec = lambda s: re.sub(r'\\u([0-9a-fA-F]{4})', lambda x: chr(int(x.group(1), 16)), s)
for word in ['哥布林', '平局', '网吧点数', '加速外挂', '错误27', '金钥匙']:
    pat = ''.join('\\u%04x' % ord(c) for c in word)
    hits = [m.start() for m in re.finditer(re.escape(pat), t)]
    print(word, '->', len(hits), 'hit(s)')
    for i in hits[:2]:
        line = t[max(0, i-40):i+80].split('\n')[-1]
        print('   ', dec(line)[:110])
