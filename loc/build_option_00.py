# -*- coding: utf-8 -*-
import json, io, os, re, sys

BASE = os.path.dirname(os.path.abspath(__file__))
chunk_path = os.path.join(BASE, 'chunks', 'option_00.json')
out_path = os.path.join(BASE, 'out', 'option_00.json')

T = {
0: "%0.2%%概率无视敌人防御。",
1: "%0.2%%概率造成双倍伤害。",
2: "%0.2%%概率造成三倍伤害。",
3: "%0.2%%概率完全恢复HP。",
4: "%0.2%%概率完全恢复魔法值。",
5: "每%0.2f点HP，防御成功率提升1点。",
6: "装备双手剑时，PvP攻击力额外提升 %0.2f。",
7: "%0.2f%%概率在10秒内每秒造成相当于力量值10%%的伤害。",
8: "%0.2f%%概率完全恢复SD。",
9: "%0.2f%%概率受击时反弹等量伤害。",
10: "%0.2f%%概率使目标一件防具的耐久度降低10%%。",
11: "%0.2f%%概率攻击目标时完全恢复SD。",
12: "%0.2f%%概率使毁灭一击的目标定身3秒。",
13: "%0.2f%%概率受击时生成无敌盾墙。#持续5秒。",
14: "%0.2f%%概率击退目标。",
15: "%0.2f%%概率使毁灭一击目标移动速度降低50%%，持续5秒。",
16: "装备战锤时，%0.2f%%概率使目标眩晕2秒。",
17: "%0.2f%%概率使目标眩晕2秒。",
18: "%0.2f%%概率使破坏一击的目标眩晕2秒。",
19: "阶级%d，技能等级：%d/10",
20: "阶级%d，技能等级：%d/20",
21: "需要5阶「最小攻击力增加」10级以上",
22: "需要7阶「HP完全恢复」10级以上",
25: "(等级类)攻击/魔力提升",
26: "+3%%生命回复/+100生命值",
49: "0级以上",
52: "10级以上",
54: "100%%EXP提升/100%%道具掉落率/一定时间内体力不会下降",
70: "50%%EXP提升/0%%道具掉落率",
71: "50%%EXP提升/50%%道具掉落率",
109: "AG消耗减少",
110: "AG恢复速度提升",
111: "AG恢复速度提升+10",
112: "AG恢复速度提升+8/雷冰抗性提升/攻击速度提升+20",
113: "AG值提升",
144: "敏捷状态+50",
145: "敏捷属性提升 %0.2f。",
146: "阿格尼斯的",
172: "狼魂祭坛围攻状态",
173: "狼魂祭坛契约状态",
174: "狼魂祭坛契约尝试",
175: "狼魂祭坛契约已禁用",
176: "狼魂祭坛契约已启用",
177: "狼魂祭坛英雄契约状态",
179: "阿米斯的",
188: "无名者的",
190: "阿努比斯的",
191: "阿波罗的",
203: "阿尔戈的",
209: "不消耗箭矢，/并提升伤害。",
210: "箭矢不会被消耗。",
211: "阿鲁安的",
213: "升华",
269: "攻击提升技能效果提升%0.2f%%，持续时间随技能等级增加。",
272: "攻击力提升(最小,最大)",
274: "攻击力提升20%%",
275: "攻击力+30",
276: "攻击力与防御力提升",
277: "攻击力提升",
278: "攻击力提升+25",
279: "攻击力提升+30",
280: "攻击力提升+40",
281: "装备毁灭之翼时，攻击力提升 %0.2f。",
282: "装备权杖时，攻击力提升 %0.2f。",
283: "装备帝王披风时，攻击力提升 %0.2f。",
284: "装备幻影之翼时，攻击力提升 %0.2f。",
285: "攻击力提升 %0.2f#装备风暴之翼时。",
286: "攻击力提升 %0.2f。",
287: "攻击力提升。",
288: "攻击力降低",
289: "攻击成功率提升",
290: "攻击速度+15",
291: "攻击速度提升",
292: "攻击速度提升+10",
293: "攻击速度提升+15",
294: "装备弓时，攻击速度提升 %0.2f。",
295: "装备单手法杖时，攻击速度提升 %0.2f。",
296: "装备单手剑时，攻击速度提升 %0.2f。",
297: "装备异界古书时，攻击速度提升 %0.2f。",
298: "攻击成功率提升(PvP)",
299: "攻击成功率提升 %0.2f。",
300: "攻击成功率提升(PvP)",
301: "攻击/魔力提升",
303: "AG自动恢复速度提升%0.2f%%。",
305: "HP自动恢复速度提升%0.2f%%。",
306: "生命自动回复提升",
308: "魔法自动回复提升",
309: "魔法自动恢复速度提升%0.2f%%。",
310: "SD自动恢复速度提升%0.2f%%。",
319: "巴纳克的",
339: "狂战士技能额外提升攻击速度与魔力%0.2f%%。",
340: "狂战士技能使治愈、魔力与攻击力提升 %0.2f。",
341: "狂战士技能使诅咒伤害额外提升%0.2f%%。",
342: "狂战士的",
343: "围攻公会1",
344: "围攻公会2",
345: "围攻公会3",
357: "流血",
361: "祝福技能使全属性提升 %0.2f。",
362: "圣诞祝福",
366: "格挡率提升",
383: "血嚎",
389: "血风暴技能伤害提升%0.2f。",
436: "布罗伊的",
463: "卡斯托尔的",
466: "刻托的",
474: "连锁闪电技能伤害提升%0.2f。",
476: "查默尔的",
477: "无视敌人防御并造成伤害的几率提升%0.2f%%。",
515: "混乱灾祸技能伤害提升%0.2f。",
537: "克罗诺的",
547: "克劳德的",
548: "寒冷",
552: "连击技能伤害提升%0.2f%%。",
558: "彗星陨落技能伤害提升%0.2f。",
560: "统率属性提升 %0.2f。",
601: "控制状态+50",
612: "致命伤害提升技能使致命伤害几率额外增加%0.2f%%。",
613: "致命伤害提升技能使卓越伤害几率额外增加%0.2f%%。",
614: "致命伤害提升技能使致命伤害增加 %0.2f。",
}

with io.open(chunk_path, encoding='utf-8') as f:
    chunk = json.load(f)
items = chunk['items']

results = [{"id": it["id"], "zh": T[it["id"]]} for it in items]
with io.open(out_path, 'w', encoding='utf-8') as f:
    json.dump({"results": results}, f, ensure_ascii=False, indent=1)

# ---------------- verification ----------------
errors = []
# 1. count / ids
src_ids = [it["id"] for it in items]
out_ids = [r["id"] for r in results]
if len(results) != len(items):
    errors.append("count mismatch: %d vs %d" % (len(results), len(items)))
if out_ids != src_ids:
    errors.append("id order/match mismatch")
missing = set(src_ids) - set(T.keys())
extra = set(T.keys()) - set(src_ids)
if missing: errors.append("missing ids: %s" % sorted(missing))
if extra: errors.append("extra ids: %s" % sorted(extra))

# traditional-character blacklist
trad = set("劍師裝備遊戲傷禦戰擊護經驗獲寶靈創亞峽穀場鬥導喚術槍騎羅倫蹤貝魯遺廢達爾澤據競惡廣廟國誕試煉長錘權鎧鋒銳龍階級態狀約壇圍會鮮閃電鎖連隕禍災統項鏈製暈續牆敵雙視點時當於內標獸風雲聖導喚書")

for it in items:
    i = it["id"]; en = it["en"]; zh = T[i]; budget = it["budget"]
    b = len(zh.encode('utf-8'))
    if b >= budget:
        errors.append("id %d: bytes %d >= budget %d | %s" % (i, b, budget, zh))
    for ch_name, cnt_en, cnt_zh in [
        ("%", en.count('%'), zh.count('%')),
        ("{", en.count('{'), zh.count('{')),
        ("#", en.count('#'), zh.count('#')),
        ("/", en.count('/'), zh.count('/')),
    ]:
        if cnt_en != cnt_zh:
            errors.append("id %d: '%s' count en=%d zh=%d | %s" % (i, ch_name, cnt_en, cnt_zh, zh))
    # placeholder token multiset
    pat = re.compile(r'%\d*\.\d*[fF%]?|%[sdufx%]|\{[0-9]+\}')
    te = pat.findall(en); tz = pat.findall(zh)
    if sorted(te) != sorted(tz):
        errors.append("id %d: placeholder tokens %s vs %s" % (i, te, tz))
    bad = [c for c in zh if c in trad]
    if bad:
        errors.append("id %d: traditional chars %s | %s" % (i, set(bad), zh))

print("items:", len(items), "results:", len(results))
print("output:", out_path)
if errors:
    print("FAILURES:")
    for e in errors:
        print(" -", e)
    sys.exit(1)
print("ALL CHECKS PASSED")
