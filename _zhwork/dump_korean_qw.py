# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
import sys, os, struct
sys.path.insert(0, config.LOC)
from bmd import read_questwords
import formats

TARGETS = {6006,6012,6018,6024,6030,6036,6042,6048,6054,6060,6066,6072,
           7001,7002,7003,7004,7005,7006,7007,7008,7009,7010,7011,7012,7013,
           7014,7015,7016,7017,7018,7019,7020,7021,7022,7023,7024,7025,7026,
           7027,7028,7029,7030,7801,7802,7803,7804,7805,7806,7901,7902,7903,
           20108,20114,20120}

def load(path):
    recs, _ = read_questwords(path)
    return dict(recs)

chs = load(os.path.join(formats.CHS, 'QuestWords_chs.bmd'))
eng_path = os.path.join(formats.ENG, 'QuestWords_eng.bmd')
eng = load(eng_path) if os.path.exists(eng_path) else {}
out = open(os.path.join(W, 'korean_qw.txt'), 'w', encoding='utf-8')
for idx in sorted(TARGETS):
    raw = chs.get(idx, b'')
    ko = raw.decode('cp949', errors='replace').rstrip('\x00')
    en = eng.get(idx, b'').decode('utf-8', errors='replace').rstrip('\x00') if idx in eng else ''
    out.write(f'=== {idx}\nKO: {ko}\nEN: {en}\n\n')
out.close()
print('eng file exists:', bool(eng), os.path.exists(eng_path), eng_path)
