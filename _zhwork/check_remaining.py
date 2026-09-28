# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
import re
p = os.path.join(config.LOCALIZATION, 'Game.zh-CN.resx')
t = open(p, encoding='utf-8').read()
for m in re.finditer(r'<data name="([^"]+)"[^>]*>(.*?)</data>', t, re.S):
    name, body = m.group(1), m.group(2)
    vm = re.search(r'<value>(.*?)</value>', body, re.S)
    v = vm.group(1) if vm else ''
    if '[error' in v:
        print('VALUE REMAINS:', name, '=>', v[:100])
# also list where the 17 occurrences live
for m in re.finditer(r'\[error', t):
    line = t.count('\n', 0, m.start()) + 1
    seg = t[m.start()-30:m.start()+30].replace('\n', ' ')
    print(line, seg)
