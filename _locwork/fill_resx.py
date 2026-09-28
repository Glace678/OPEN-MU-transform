# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Fill remaining missing/empty resx entries so every locale has full key parity."""
import xml.etree.ElementTree as ET, os, re

LOC = config.LOCALIZATION

def load(g,l):
    p=os.path.join(LOC,f'{g}.{l}.resx')
    root=ET.parse(p).getroot()
    d={}
    order=[]
    for x in root.findall('data'):
        k=x.get('name'); v=x.find('value'); c=x.find('comment')
        d[k]=(v.text if v is not None else '', c.text if c is not None else None)
        order.append(k)
    return d

# ---- explicit translations for genuinely visible labels ----
T = {
'de': {'Gamepad':'Gamepad','Haptics':'Vibration','GamepadActionHelper':'MU-Helfer',
       'SoloShopSkills':'Fertigkeiten','OnePercentLow':'1 % Low'},
'es': {'GamepadActionHelper':'Ayudante de MU','OnePercentLow':'1 % bajo',
       'Text_0':'¡Sí! ¿Necesitas algo?'},
'id': {'Gamepad':'Gamepad','GamepadActionHelper':'Pembantu MU','OnePercentLow':'1% Rendah'},
'ja': {'Gamepad':'ゲームパッド','Haptics':'振動','GamepadActionHelper':'MUヘルパー',
       'SoloShopSkills':'スキル','OnePercentLow':'1% Low',
       'Valhalla':'ヴァルハラ','Helheim':'ヘルヘイム','Midgard':'ミッドガルド','Kara':'カラ',
       'Lamu':'ラム','Nacal':'ナカル','Rasa':'ラサ','Rance':'ランス','Tarh':'ター','Uz':'ウズ',
       'Moz':'モズ','Luga(Lamu2)':'ルーガ(ラム2)','Titan':'タイタン','Elca':'エルカ','test':'テスト',
       'Phoenix Shot (Mana:%d)':'フェニックスショット（マナ:%d）'},
'pl': {'GamepadActionHelper':'pomocnik MU','OnePercentLow':'Min. 1%'},
'pt': {'GamepadActionHelper':'Auxiliar do MU','OnePercentLow':'1% baixo',
       'Text_0':'Sim! Precisa de algo?'},
'ru': {'GamepadActionHelper':'помощник MU','OnePercentLow':'1% минимум'},
'tl': {'JToggleChatCommands':'J : I-on/I-off ang mga chat command','VerticalSync':'Vertical sync',
       'AverageFrameRate':'Average FPS','OnePercentLow':'1% Mababa','Gamepad':'Gamepad',
       'Haptics':'Vibration','FrameLimitFrameLimiter':'Frame limiter',
       'FrameLimitVerticalSync':'Vertical sync','GamepadActionQuickItem1':'Quick item 1',
       'GamepadActionQuickItem2':'Quick item 2','GamepadActionQuickItem3':'Quick item 3',
       'GamepadActionQuickItem4':'Quick item 4','GamepadActionMenu':'System menu',
       'GamepadActionHelper':'Katulong ng MU','SoloShopTitle':'Solo Shop'},
'uk': {'GamepadActionHelper':'помічник MU','OnePercentLow':'1% мінімум'},
}

def esc(s):
    return (s.replace('&','&amp;').replace('<','&lt;').replace('>','&gt;'))

for l in ['de','es','id','ja','pl','pt','ru','tl','uk']:
    for group in ['Game','Dialog']:
        en=load(group,'en'); cur=load(group,l)
        missing=[k for k in en if k not in cur]
        # empties that have English content
        empties=[k for k,v in cur.items() if k in en and not (v[0] or '').strip() and (en[k][0] or '').strip()]
        add = missing + [k for k in empties if k not in missing]
        if not add: continue
        p=os.path.join(LOC,f'{group}.{l}.resx')
        text=open(p,encoding='utf-8').read()
        if not text.endswith('\n'): text+='\n'
        blocks=[]
        tab=T.get(l,{})
        for k in add:
            enval, comment = en[k]
            if k in tab: val=tab[k]
            else: val=enval  # verbatim for tokens / legacy / proper nouns
            cmt = f'\n    <comment>{esc(comment)}</comment>' if comment else ''
            blocks.append(f'  <data name="{esc(k)}" xml:space="preserve">\n'
                          f'    <value>{esc(val)}</value>{cmt}\n  </data>\n')
        text=text.replace('</root>', ''.join(blocks)+'</root>')
        open(p,'w',encoding='utf-8',newline='').write(text)
        print(l,group,'added',len(add))
