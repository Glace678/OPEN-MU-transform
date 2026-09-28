# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Second-pass scan: TW-flavored words, duplicates, spacing, residual English sentences."""
import re, os, glob

LOC = config.LOCALIZATION
WORDS = ['巡览', '重设', '拷贝', '全屏幕', '荧幕', '萤幕', '程式', '档案',
         '滑鼠', '预设', '软体', '硬体', '资讯', '网路', '线上', '登入', '登出',
         '储存', '搜寻', '实作', '运作', '检视', '汇出', '汇入', '回应', '录影',
         '音效', '视窗', '数据机', '行动', '塑胶', '宝特', '列印', '免洗',
         '咖', '网咖', '褓姆', '游民', '游乐器', '主控台', '资料', '位元',
         '组態', '组态', '当机', '滑杆', '卷轴', '匣', '光碟', '磁碟',
         '资料夹', '控制台', '行动电话', '简讯', '影片', '數位', '类別']

out = open(os.path.join(W, 'zh_scan2.txt'), 'w', encoding='utf-8')
for f in sorted(glob.glob(os.path.join(LOC, '*.zh-CN.resx'))):
    base = os.path.basename(f)
    txt = open(f, encoding='utf-8').read()
    for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', txt, re.S):
        name, v = m.group(1), m.group(2)
        hits = [w for w in WORDS if w in v]
        if hits:
            out.write(f"[{base}] {name}: {hits}\n   {v[:200]}\n")
out.close()
print('ok')
