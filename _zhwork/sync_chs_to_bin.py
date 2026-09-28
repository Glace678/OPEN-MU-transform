# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Stage the Chs data language set into MuMain/src/bin/Data/Local/Chs
(the game-data.zip source), excluding backups and non-language leftovers."""
import os, shutil

SRC = config.SERVER.chs
DST = os.path.join(config.DEV_TREE, 'Data', 'Local', 'Chs')

FILES = [
    'BuffEffect_chs.bmd',
    'ItemLevelTooltip_chs.bmd',
    'ItemTooltipText_chs.bmd',
    'ItemTooltip_chs.bmd',
    'JewelOfHarmonyOption_chs.bmd',
    'JewelOfHarmonySmelt_chs.bmd',
    'MasterSkillTooltip_chs.bmd',
    'MoveReq_chs.bmd',
    'NpcName_Chs.txt',
    'QuestWords_chs.bmd',
    'Quest_chs.bmd',
    'item_chs.bmd',
    'itemsetoption_chs.bmd',
    'skill_chs.bmd',
    'slide_chs.bmd',
    'socketitem_chs.bmd',
]
SUBDIRS = ['Minimap', 'ImgsMapName']

os.makedirs(DST, exist_ok=True)
n = 0
for name in FILES:
    s = os.path.join(SRC, name)
    assert os.path.isfile(s), f'missing {s}'
    shutil.copy2(s, os.path.join(DST, name))
    n += 1

for sub in SUBDIRS:
    sd = os.path.join(SRC, sub)
    dd = os.path.join(DST, sub)
    os.makedirs(dd, exist_ok=True)
    for name in os.listdir(sd):
        if name.endswith('.bak-en'):
            continue
        s = os.path.join(sd, name)
        if os.path.isfile(s):
            shutil.copy2(s, os.path.join(dd, name))
            n += 1

print(f'staged {n} files to {DST}')
