# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Apply zh-CN proofreading fixes inside <value> bodies only. Idempotent-ish:
asserts exact hit counts; aborts (without writing) if any expectation fails."""
import re, os, shutil, sys

LOC = config.LOCALIZATION

# (old, new, expected_hits) — applied to every <value> body
GAME_SUBS = [
    ('哥布尔', '哥布林', 7),
    ('与密钥匙组合来创建金盒子', '与金钥匙组合来创建金盒', 1),
    ('与银钥匙组合来创建银盒子', '与银钥匙组合来创建银盒', 1),
    ('黑客攻击', '外挂作弊', 1),
    ('黑客或电脑病毒测试', '外挂或电脑病毒检测', 1),
    ('黑客工具检查程序', '外挂工具检查程序', 1),
    ('已发现黑客工具。如果您没有使用黑客工具', '已发现外挂工具。如果您没有使用外挂工具', 1),
    ('检测到黑客工具（%s）', '检测到外挂工具（%s）', 2),
    ('检测到速度黑客', '检测到加速外挂', 1),
    ('检测到游戏黑客（%d）', '检测到游戏外挂（%d）', 1),
    ('[错误6]No.', '[错误6] No.', 1),
    ('使用技能热键', '使用技能快捷键', 1),
    ('聊天模式热键', '聊天模式快捷键', 1),
    ('啊!伟大的战士', '啊！伟大的战士', 1),
    ('迪诺兰特, +10, +15 物品, 透明披风', '迪诺兰特、+10、+15 物品、透明披风', 1),
    ('PC咖啡厅点(%d/%d)', '网吧点数（%d/%d）', 1),
    ('PC咖啡厅积分店', '网吧点数商店', 1),
    ('PC咖啡馆点商店允许您只购买物品。', '网吧点数商店仅可购买指定物品。', 1),
    ('电脑咖啡馆', '网吧', 1),
    ('100%中奖卡', '100%%中奖卡', 2),
    ('全屏幕模式', '全屏模式', 1),
    ('%s 状态已在 %d 处重设。', '%s 状态已在 %d 处重置。', 1),
    ('非法警告', '亡命之徒警告', 1),
    ('上一页行动', '上一姿势', 1),
    ('下一步行动', '下一姿势', 1),
]
# whole-value exact replacements
GAME_EXACT = {
    'ETC。': '其他',
    '热键': '快捷键',
    '绑起来！！！': '平局！！！',
    '!!警告 ！！': '警告！！',
}

EDITOR_SUBS = [
    ('重设偏移', '重置偏移', 1),
    ('重设距离', '重置距离', 1),
    ('重设为相机默认值', '重置为相机默认值', 1),
    ('重设为自然视锥', '重置为自然视锥', 1),
    ('开始巡览', '开始巡游', 1),
    ('拷贝调试信息至剪贴板', '复制调试信息至剪贴板', 1),
    ('调试信息已拷贝至剪贴板', '调试信息已复制至剪贴板', 1),
    ('全屏幕', '全屏', 1),
    ('装备中物品', '已装备物品', 1),
    ('固定 索引/名称', '冻结索引/名称', 1),
    ('雾打开', '雾开启', 1),
    ('(追踪所视对象)', '（追踪所视对象）', 1),
]
EDITOR_EXACT = {
    '保存成功!': '保存成功！',
    '保存失败!': '保存失败！',
    '技能保存成功!': '技能保存成功！',
    '技能保存失败!': '技能保存失败！',
    '导出 S6E3 成功!': '导出 S6E3 成功！',
    '导出 S6E3 格式失败!': '导出 S6E3 格式失败！',
    '导出 CSV 成功!': '导出 CSV 成功！',
    '导出 CSV 失败!': '导出 CSV 失败！',
    '技能导出 CSV 成功!': '技能导出 CSV 成功！',
    '技能导出 CSV 失败!': '技能导出 CSV 失败！',
    '找不到文件: {0}': '找不到文件：{0}',
    '无效索引: {0}': '无效索引：{0}',
    '错误: 索引 {0} 已被使用!': '错误：索引 {0} 已被使用！',
    '搜索:': '搜索：',
    "未选择任何字段。请点击 '字段' 以显示字段.": "未选择任何字段。请点击“字段”以显示字段。",
    '切换字段显示状态:': '切换字段显示状态：',
    '物品保存成功!': '物品保存成功！',
    '导出旧版格式成功!': '导出旧版格式成功！',
    '导出旧版格式失败!': '导出旧版格式失败！',
    '物品导出 CSV 成功!': '物品导出 CSV 成功！',
    '物品属性导出 CSV 失败!': '物品属性导出 CSV 失败！',
    '技能属性导出 CSV 失败!': '技能属性导出 CSV 失败！',
    '搜索技能:': '搜索技能：',
    '绘制距离:': '绘制距离：',
    '视角对齐: 跟随相机的偏航与俯仰 （追踪所视对象）。': '视角对齐：跟随相机的偏航与俯仰（追踪所视对象）。',
    '调试可视化:': '调试可视化：',
    '绘制:': '绘制：',
    '待办 - 尚未正常运作:': '待办 - 暂不可用：',
    '待办 - 尚未实作:': '待办 - 尚未实现：',
    '警告: 检测到窗口尺寸不一致！': '警告：检测到窗口尺寸不一致！',
    '巡览': '巡游',
}

VAL_RE = re.compile(r'(<value>)(.*?)(</value>)', re.S)

def transform(path, subs, exact, bang_prefix_errors=False):
    raw = open(path, encoding='utf-8', newline='').read()
    counts = {old: 0 for old, _, _ in subs}
    exact_counts = {k: 0 for k in exact}
    error_prefix = 0

    def repl(m):
        nonlocal error_prefix
        pre, body, post = m.group(1), m.group(2), m.group(3)
        for old, new, _ in subs:
            n = body.count(old)
            if n:
                counts[old] += n
                body = body.replace(old, new)
        # compress spaces between full-width bangs: ！ ！ ！ -> ！！！
        body, n = re.subn(r'(?<=！)[\s　]+(?=！)', '', body)
        if n:
            counts.setdefault('__bangspace__', 0)
            counts['__bangspace__'] += n
        if body in exact:
            exact_counts[body] += 1
            body = exact[body]
        # unify legacy [errorN] markers to Chinese within display values
        body2, n = re.subn(r'\[error(?=\d)', '[错误', body)
        if n:
            error_prefix += n
            body = body2
        return pre + body + post

    new = VAL_RE.sub(repl, raw)

    ok = True
    for old, _, exp in subs:
        if counts[old] != exp:
            print(f"COUNT MISMATCH {os.path.basename(path)}: {old!r} got {counts[old]} expected {exp}")
            ok = False
    for k, v in exact_counts.items():
        if v != 1:
            print(f"EXACT MISMATCH {os.path.basename(path)}: {k!r} hit {v}")
            ok = False
    if bang_prefix_errors:
        print(f"  [errorN] -> [错误N]: {error_prefix} markers")
    print(f"  bang-space compressed: {counts.get('__bangspace__', 0)}")
    if not ok:
        return False
    bak = path + '.bak-zhproof'
    if not os.path.exists(bak):
        shutil.copy2(path, bak)
    with open(path, 'w', encoding='utf-8', newline='') as fh:
        fh.write(new)
    return True

print('Game.zh-CN.resx')
g = transform(os.path.join(LOC, 'Game.zh-CN.resx'), GAME_SUBS, GAME_EXACT, True)
print('Editor.zh-CN.resx')
e = transform(os.path.join(LOC, 'Editor.zh-CN.resx'), EDITOR_SUBS, EDITOR_EXACT)
if not (g and e):
    sys.exit('ABORTED: expectations failed, no files written')
print('APPLIED')
