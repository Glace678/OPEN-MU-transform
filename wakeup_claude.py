# -*- coding: utf-8 -*-
import os as _os, sys as _sys
import config
W = _os.path.dirname(_os.path.abspath(__file__))
r"""
Claude Code 任务唤醒与恢复脚本 (Wakeup & Resume Script)
用于在指定时间（明天凌晨 2:00）自动唤醒因 429 限流被中断的 Claude Code 任务，
并切换为 ark-code-latest 模型的 max 思考模式继续执行。

目标任务详情:
- 工作目录: D:\openmu自用
- 会话 ID: 695b9855-eed5-4fd2-a4fc-7816b377c348
- 目标模型: ark-code-latest
- 思考模式: max (effort: max)
"""

import sys
import os
import json
import time
import datetime
import subprocess
import shutil

TARGET_DIR = config.WORKSPACE
SESSION_ID = "695b9855-eed5-4fd2-a4fc-7816b377c348"
MODEL_NAME = "ark-code-latest"
EFFORT_LEVEL = "max"
PROMPT = (
    "继续完成上一轮未完成的游戏本地化任务。"
    "注意：上一轮因为同时并发启动了21个子agent导致触发火山引擎 429 限流错误。"
    "本次继续执行请严格控制并发（建议顺序执行或保持最多2-3个并发），"
    "继续检查并完成英文 NPC 名字、任务描述、以及切换其它语言（如日语）时的设置界面与全部内容本地化，并进行构建验证。"
)

SETTINGS_FILE = os.path.expanduser(r"~\.claude\settings.json")


def ensure_claude_settings():
    """确保 ~/.claude/settings.json 配置了 ark-code-latest 与 max 思考深度"""
    if not os.path.exists(SETTINGS_FILE):
        print(f"[!] 警告: 未找到 settings.json: {SETTINGS_FILE}")
        return

    try:
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            settings = json.load(f)

        modified = False
        if settings.get("model") != MODEL_NAME:
            settings["model"] = MODEL_NAME
            modified = True

        if "modelSettings" not in settings:
            settings["modelSettings"] = {}

        if settings["modelSettings"].get(MODEL_NAME, {}).get("effortLevel") != EFFORT_LEVEL:
            settings["modelSettings"][MODEL_NAME] = {"effortLevel": EFFORT_LEVEL}
            modified = True

        if modified:
            with open(SETTINGS_FILE, "w", encoding="utf-8") as f:
                json.dump(settings, f, indent=2, ensure_ascii=False)
            print(f"[✓] 已更新 {SETTINGS_FILE}：模型设为 {MODEL_NAME}，思考级别设为 {EFFORT_LEVEL}")
        else:
            print(f"[✓] {SETTINGS_FILE} 已是 {MODEL_NAME} (effort: {EFFORT_LEVEL})")
    except Exception as e:
        print(f"[!] 读取或更新 settings.json 失败: {e}")


def get_target_wakeup_time(target_hour=4, target_minute=0):
    """计算下一个目标唤醒时间（默认为今天/明天凌晨 4:00）"""
    now = datetime.datetime.now()
    target = now.replace(hour=target_hour, minute=target_minute, second=0, microsecond=0)
    # 如果当前时间已经过了今天的 target_hour:target_minute，目标就是明天的该时间
    if now >= target:
        target += datetime.timedelta(days=1)
    return target


def wait_until(target_time):
    """倒计时等待直到目标时间"""
    print("=" * 65)
    print("           Claude Code 任务定时唤醒监控程序")
    print("=" * 65)
    print(f"[*] 当前系统时间:   {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"[*] 计划唤醒时间:   {target_time.strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"[*] 目标项目目录:   {TARGET_DIR}")
    print(f"[*] 恢复会话 ID:    {SESSION_ID}")
    print(f"[*] 选用执行模型:   {MODEL_NAME}")
    print(f"[*] 思考模式级别:   {EFFORT_LEVEL}")
    print("=" * 65)

    ensure_claude_settings()

    while True:
        now = datetime.datetime.now()
        remaining = (target_time - now).total_seconds()
        if remaining <= 0:
            print("\n[★] 唤醒时间已到达！正在启动 Claude Code 恢复任务...")
            break

        hours = int(remaining // 3600)
        minutes = int((remaining % 3600) // 60)
        seconds = int(remaining % 60)

        sys.stdout.write(f"\r[⏳] 距离唤醒还剩: {hours:02d}小时 {minutes:02d}分 {seconds:02d}秒 (按 Ctrl+C 可取消等待)...")
        sys.stdout.flush()

        # 根据剩余时间调整 sleep 间隔
        sleep_interval = 1.0 if remaining < 60 else (5.0 if remaining < 3600 else 15.0)
        time.sleep(sleep_interval)


def launch_claude(interactive=True):
    """执行唤醒启动命令"""
    ensure_claude_settings()

    # 找到 claude 命令的路径
    claude_cmd = shutil.which("claude") or "claude"

    # 构建启动命令
    cmd_args = [
        claude_cmd,
        "--resume", SESSION_ID,
        "--model", MODEL_NAME,
        "--effort", EFFORT_LEVEL,
        "--dangerously-skip-permissions",
        PROMPT
    ]

    log_file = os.path.join(TARGET_DIR, f"claude_resume_{datetime.datetime.now().strftime('%Y%m%d_%H%M%S')}.log")

    print(f"[*] 工作目录: {TARGET_DIR}")
    print(f"[*] 执行命令: {' '.join(cmd_args[:5])} ...")
    print(f"[*] 执行日志: {log_file}")

    env = os.environ.copy()
    env["ANTHROPIC_MODEL"] = MODEL_NAME

    if interactive:
        # 在新的独立 PowerShell 窗口中运行，带 -NoExit，完成后保留窗口方便查看
        escaped_prompt = PROMPT.replace('"', '`"').replace("'", "''")
        ps_command = (
            f"cd '{TARGET_DIR}'; "
            f"$env:ANTHROPIC_MODEL='{MODEL_NAME}'; "
            f"Write-Host '[*] 正在恢复 Claude Code 任务 (Model: {MODEL_NAME}, Effort: {EFFORT_LEVEL})...' -ForegroundColor Cyan; "
            f"claude --resume {SESSION_ID} --model {MODEL_NAME} --effort {EFFORT_LEVEL} --dangerously-skip-permissions '{escaped_prompt}'"
        )
        
        launch_args = [
            "powershell.exe",
            "-NoProfile",
            "-NoExit",
            "-ExecutionPolicy", "Bypass",
            "-Command", ps_command
        ]
        
        print("[*] 正在拉起前台交互式 PowerShell 窗口...")
        # 启动独立控制台窗口
        subprocess.Popen(
            launch_args,
            cwd=TARGET_DIR,
            creationflags=subprocess.CREATE_NEW_CONSOLE,
            env=env
        )
        print("[✓] 任务窗口已成功拉起并保持运行！你可以在明早直接在窗口中查看结果。")
    else:
        # 后台静默执行，输出写入日志
        print("[*] 正在后台启动任务并写入日志...")
        with open(log_file, "w", encoding="utf-8") as out:
            proc = subprocess.Popen(
                cmd_args,
                cwd=TARGET_DIR,
                stdout=out,
                stderr=subprocess.STDOUT,
                env=env
            )
            print(f"[✓] 后台进程已启动，PID: {proc.pid}，日志见: {log_file}")


def main():
    # 支持命令行参数: --now 立即执行(测试用), --headless 后台模式
    run_now = "--now" in sys.argv
    headless = "--headless" in sys.argv
    interactive = not headless

    if run_now:
        print("[*] 收到 --now 参数，跳过等待，立即唤醒执行！")
        launch_claude(interactive=interactive)
        return

    # 默认唤醒目标: 凌晨 4:00
    target_time = get_target_wakeup_time(target_hour=4, target_minute=0)
    
    # 如果用户通过命令行指定了时间参数，例如 02:00
    for arg in sys.argv[1:]:
        if ":" in arg and not arg.startswith("--"):
            try:
                parts = arg.split(":")
                h, m = int(parts[0]), int(parts[1])
                target_time = get_target_wakeup_time(target_hour=h, target_minute=m)
                break
            except Exception:
                pass

    wait_until(target_time)
    launch_claude(interactive=interactive)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n[!] 用户中断操作，等待已取消。")
        sys.exit(0)
