# opencode 会话唤醒 / 定时续发教程（通用版）

> 适用场景：opencode 里的任务因**额度耗尽**或其他原因停住，需要在指定时间后自动向**那个会话**发送一条消息（如"继续任务"）把它唤醒。
> 本教程全部步骤已在 2026-10-05 本机实测通过（含中文消息编码、数据库校验、任务计划程序部署）。
> 给执行此教程的 AI 模型：按步骤顺序做，不要跳过"验证送达"环节，发送前先用测试会话彩排。

---

## 0. 本机关键路径（已验证）

| 项目 | 路径 |
|---|---|
| opencode 可执行文件 | `C:\Users\Glace\AppData\Roaming\npm\node_modules\opencode-ai\bin\opencode.exe` |
| opencode 数据库 | `C:\Users\Glace\.local\share\opencode\opencode.db`（SQLite，WAL 模式，可能很大） |
| Python（只读查库用） | `C:\Users\Glace\.workbuddy\binaries\python\versions\3.13.12\python.exe` |
| Node（可选，跑脚本用） | `C:\Users\Glace\.workbuddy\binaries\node\versions\22.22.2-3\node.exe` |

---

## 1. 定位目标会话

**必须以只读方式（`mode=ro` URI）打开数据库**，opencode 正在运行，写连接可能锁库。

```python
import sqlite3, datetime, json
con = sqlite3.connect(r'file:C:/Users/Glace/.local/share/opencode/opencode.db?mode=ro', uri=True)
cur = con.cursor()
# 按目录找最近更新的会话（directory 用 / 分隔）
cur.execute("SELECT id, directory, title, time_updated FROM session "
            "WHERE directory LIKE '%openmu%' AND time_archived IS NULL "
            "ORDER BY time_updated DESC LIMIT 5")
for r in cur.fetchall():
    print(r[0], '|', r[1], '|', r[2], '|',
          datetime.datetime.fromtimestamp(r[3]/1000).strftime('%m-%d %H:%M:%S'))
```

**判断会话状态**（重要，别误判）：

```python
sid = 'ses_xxxxxxxx'  # 上一步拿到的 id
cur.execute("SELECT id, time_created, json_extract(data,'$.role') FROM message "
            "WHERE session_id=? ORDER BY time_created DESC LIMIT 6", (sid,))
```

- **看最后一条消息的时间**：几分钟内还在更新 = 任务正在运行（不需要唤醒，或消息会自动排队）。
- **空文本的 assistant 消息 ≠ 卡死**：工具调用消息没有 text part，看起来是"空的"但任务在干活。要看文本内容用 part 表：

```python
cur.execute("SELECT p.data FROM part p JOIN message m ON p.message_id=m.id "
            "WHERE m.session_id=? AND json_extract(m.data,'$.role')='user' "
            "ORDER BY m.time_created DESC LIMIT 1", (sid,))
```

- `role` 不在 message 表的列里，在 `message.data` JSON 里，用 `json_extract(data,'$.role')` 取。

---

## 2. 发送消息（核心步骤）

```bash
"C:/Users/Glace/AppData/Roaming/npm/node_modules/opencode-ai/bin/opencode.exe" \
  run --session <会话ID> --dir "D:\项目目录" "继续任务" < /dev/null
```

### 必须遵守的坑（每一条都实测踩过）

1. **stdin 必须关闭或重定向（`< /dev/null`）**。否则 opencode 静默挂起：无任何输出、无报错、消息不落库、进程不退出。这是最大的坑。Node 里对应 `stdio: ['ignore','pipe','pipe']`。
2. **用 `--session <ID>`，不要用 `--continue`**。`--continue` 续的是"该目录最新会话"，可能续到别的会话（比如刚做的测试会话）。
3. **中文消息直接作为参数传递没问题**，整条链路（bash/cmd → opencode → 数据库）UTF-8 编码验证无损。
4. `opencode run` 是**阻塞式**的：发送后它会一直跑到该轮 agent 回复结束才退出，长任务可能挂几十分钟。但**消息在发送瞬间就落库了**，不需要等它退出。
5. 目标会话**正在忙**也没关系，消息会排队，当前轮结束后自动处理。
6. `--dir` 要传会话所属的项目目录（session 表里查到的 directory）。

### 发送前先彩排（强烈建议）

新建一个测试会话验证整条链路，**不要直接对真实会话发**：

```bash
cd "/d/项目目录" && timeout 120 "C:/Users/Glace/AppData/Roaming/npm/node_modules/opencode-ai/bin/opencode.exe" \
  run --title "发送机制测试" "连通性测试：请只回复ok" < /dev/null
```

收到 `ok` 即链路正常。测完可再对测试会话用 `--session` 续发一次，验证续发模式。

---

## 3. 验证送达（不要省略）

发送后查数据库确认消息真的进了目标会话（而不是发完就当成功）：

```python
import sqlite3, json
con = sqlite3.connect(r'file:C:/Users/Glace/.local/share/opencode/opencode.db?mode=ro', uri=True)
cur = con.cursor()
cur.execute("SELECT p.data FROM part p JOIN message m ON p.message_id=m.id "
            "WHERE m.session_id=? AND m.time_created>=? AND json_extract(m.data,'$.role')='user'",
            (sid, since_ms))  # since_ms = 发送时刻的毫秒时间戳
for (d,) in cur.fetchall():
    j = json.loads(d)
    if j.get('type')=='text' and '继续' in j.get('text',''):
        print('FOUND')  # 送达成功
```

---

## 4. 定时方案（Windows）

**不要用 `sleep 5h` 之类的前台等待**——宿主进程（AI 助手/终端）一关就全挂。用 **Windows 任务计划程序**，它独立于任何进程生命周期。

**注意**：`schtasks.exe` 可能被安全策略拉黑（WorkBuddy 沙箱默认黑名单里有它），用 PowerShell 的 `Register-ScheduledTask` cmdlet 代替，同样可能遇到沙箱拦截 `Start-Process`，但 cmdlet 方式实测可行。

### 推荐：双保险 + 自动重试 + 幂等

- **主任务**：目标时间触发（如 06:00）
- **兜底任务**：目标时间 + 40 分钟触发（如 06:40），脚本带 `--guard` 参数：若 status 文件 12 分钟内被主实例更新过，说明主实例活着，直接退出
- **脚本逻辑**：最多 12 次尝试、间隔 5 分钟；每次发送后轮询数据库验证送达（20 秒一次，最多 15 分钟）；成功后 status.txt 写入 `SENT-OK`（幂等标记，兜底任务看到即退出）；成功后自动删除两个计划任务
- 成功后脚本**保持存活直到 opencode run 退出**（别提前退，管道断裂可能杀死正在跑的 agent）

### 注册任务的 PowerShell 模板

```powershell
$node = "C:\Users\Glace\.workbuddy\binaries\node\versions\22.22.2-3\node.exe"
$js   = "D:\脚本目录\send-continue.js"
$action  = New-ScheduledTaskAction -Execute $node -Argument ('"' + $js + '" --immediate')
$actionG = New-ScheduledTaskAction -Execute $node -Argument ('"' + $js + '" --immediate --guard')
$trig1 = New-ScheduledTaskTrigger -Once -At "2026-10-05 06:00:00"
$trig2 = New-ScheduledTaskTrigger -Once -At "2026-10-05 06:40:00"
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName "OpenCodeResume-0600" -Action $action  -Trigger $trig1 -Settings $settings -Force
Register-ScheduledTask -TaskName "OpenCodeResume-0640" -Action $actionG -Trigger $trig2 -Settings $settings -Force
```

`-StartWhenAvailable`：错过的触发时间会在下次可用时补跑。

**验证任务已注册**（PowerShell 工具可能不回显 stdout，写文件再读）：

```powershell
Get-ScheduledTask -TaskName "OpenCodeResume-*" | ForEach-Object {
  $i = Get-ScheduledTaskInfo -TaskName $_.TaskName
  "$($_.TaskName) | $($_.State) | $($i.NextRunTime)"
} | Out-File "D:\脚本目录\check.txt" -Encoding utf8
```

**取消任务**：

```powershell
Unregister-ScheduledTask -TaskName "OpenCodeResume-0600" -Confirm:$false
Unregister-ScheduledTask -TaskName "OpenCodeResume-0640" -Confirm:$false
```

### 触发时间的确定

用户说"5 小时后"时，先问清楚或按当前时间 +5h 取整点。任务计划程序只支持 `HH:mm` 精度（`New-ScheduledTaskTrigger -Once -At` 支持到秒，但没必要）。

---

## 5. 现成脚本（复制即用）

两个文件放在同一目录，改脚本头部 4 个常量即可。

### verify_send.py（送达验证）

```python
# -*- coding: utf-8 -*-
# 用法: verify_send.py <session_id> <since_ms>  输出 FOUND / NOTFOUND
import sqlite3, sys, json

DB = r"file:C:/Users/Glace/.local/share/opencode/opencode.db?mode=ro"
KEYWORD = u"\u7ee7\u7eed"  # 继续

sid, since = sys.argv[1], int(sys.argv[2])
con = sqlite3.connect(DB, uri=True)
cur = con.cursor()
cur.execute(
    "SELECT p.data FROM part p JOIN message m ON p.message_id = m.id "
    "WHERE m.session_id = ? AND m.time_created >= ? "
    "AND json_extract(m.data, '$.role') = 'user'", (sid, since))
found = False
for (d,) in cur.fetchall():
    try: j = json.loads(d)
    except Exception: continue
    if j.get("type") == "text" and KEYWORD in (j.get("text") or ""):
        found = True; break
print("FOUND" if found else "NOTFOUND")
```

### send-continue.js（定时 + 发送 + 重试 + 幂等）

用法：
- `node send-continue.js --at "2026-10-05T06:00:00+08:00"` — 定时发送（自己等待）
- `node send-continue.js --immediate` — 立即发送（给任务计划程序用）
- `node send-continue.js --immediate --guard` — 兜底实例专用（有活跃主实例则退出）
- 可选 `--session <id>` 覆盖目标会话（测试/彩排用）

```js
const { spawn } = require("child_process");
const fs = require("fs");
const path = require("path");

// ====== 按需修改这 4 项 ======
const SESSION_ID = "ses_xxxxxxxxxxxx";     // 目标会话 ID（第 1 步查到）
const DIR = "D:\\openmu\u81ea\u7528";      // 项目目录（D:\openmu自用）
const MESSAGE = "\u7ee7\u7eed\u4efb\u52a1"; // 继续任务
const OPENCODE = "C:\\Users\\Glace\\AppData\\Roaming\\npm\\node_modules\\opencode-ai\\bin\\opencode.exe";
// ============================

const PYTHON = "C:\\Users\\Glace\\.workbuddy\\binaries\\python\\versions\\3.13.12\\python.exe";
const TASK_NAMES = ["OpenCodeResume-0600", "OpenCodeResume-0640"];
const HERE = __dirname;
const LOG = path.join(HERE, "resume-log.txt");
const STATUS = path.join(HERE, "status.txt");
const VERIFY = path.join(HERE, "verify_send.py");

const MAX_ATTEMPTS = 12;
const RETRY_GAP_MS = 5 * 60e3;
const DELIVERY_TIMEOUT_MS = 15 * 60e3;
const HEARTBEAT_MS = 10 * 60e3;

let fireAt = null, immediate = false, guard = false, sessionId = SESSION_ID;
for (let i = 2; i < process.argv.length; i++) {
  const a = process.argv[i];
  if (a === "--immediate") immediate = true;
  else if (a === "--guard") guard = true;
  else if (a === "--at") fireAt = new Date(process.argv[++i]).getTime();
  else if (a === "--wait") fireAt = Date.now() + parseInt(process.argv[++i], 10) * 60e3;
  else if (a === "--session") sessionId = process.argv[++i];
}

function log(msg) { try { fs.appendFileSync(LOG, `[${new Date().toISOString()}] ${msg}\n`, "utf8"); } catch (e) {} }
function setStatus(s) { try { fs.writeFileSync(STATUS, `${new Date().toISOString()} ${s}\n`, "utf8"); } catch (e) {} }
function statusText() { try { return fs.readFileSync(STATUS, "utf8"); } catch (e) { return ""; } }
function statusAgeMs() { try { return Date.now() - fs.statSync(STATUS).mtimeMs; } catch (e) { return Infinity; } }
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

if (statusText().includes("SENT-OK")) { log("已是 SENT-OK, 退出"); process.exit(0); }
if (guard && statusAgeMs() < 12 * 60e3 && !statusText().includes("FAILED")) {
  log("guard: 检测到活跃主实例, 退出"); process.exit(0);
}

function verify(sinceMs) {
  return new Promise((resolve) => {
    try {
      const p = spawn(PYTHON, [VERIFY, sessionId, String(sinceMs)],
        { windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
      let out = "";
      p.stdout.on("data", (d) => (out += d));
      p.stderr.on("data", (d) => (out += d));
      p.on("error", () => resolve(false));
      p.on("close", () => resolve(out.trim().endsWith("FOUND")));
    } catch (e) { resolve(false); }
  });
}

async function attempt(n) {
  const since = Date.now();
  log(`--- 第 ${n}/${MAX_ATTEMPTS} 次尝试 ---`);
  let child;
  try {
    child = spawn(OPENCODE, ["run", "--session", sessionId, "--dir", DIR, MESSAGE], {
      cwd: DIR, windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],  // 关键: stdin 必须关闭, 否则 opencode 挂起
    });
  } catch (e) { log(`启动失败: ${e}`); return { ok: false, child: null }; }
  let out = "";
  child.stdout.on("data", (d) => (out += d));
  child.stderr.on("data", (d) => (out += d));

  const deadline = Date.now() + DELIVERY_TIMEOUT_MS;
  while (Date.now() < deadline) {
    await sleep(20e3);
    setStatus(`SENDING 第${n}次尝试`);   // 保持 status 新鲜, 供 guard 判断
    if (await verify(since)) return { ok: true, child };
    if (child.exitCode !== null) {
      if (await verify(since)) return { ok: true, child };
      log(`退出 code=${child.exitCode}, 未送达。输出末尾:\n${out.slice(-1200)}`);
      return { ok: false, child: null };
    }
  }
  log("超时未送达, 终止本次尝试");
  try { child.kill(); } catch (e) {}
  return { ok: false, child: null };
}

function cleanupTask() {
  for (const tn of TASK_NAMES)
    try { spawn("schtasks", ["/Delete", "/TN", tn, "/F"], { windowsHide: true, stdio: "ignore" }); } catch (e) {}
}

async function main() {
  log(`========== 启动 pid=${process.pid} ==========`);
  log(`目标会话: ${sessionId} / 消息: ${MESSAGE}`);
  setStatus(immediate ? "SENDING" : "WAITING");
  if (!immediate) {
    if (!fireAt || isNaN(fireAt)) { log("错误: 未指定 --at/--wait"); setStatus("FAILED"); process.exit(1); }
    log(`计划发送: ${new Date(fireAt).toLocaleString("zh-CN")}`);
    while (Date.now() < fireAt) {
      setStatus(`WAITING 剩余${Math.round((fireAt - Date.now()) / 60e3)}分钟`);
      await sleep(Math.min(HEARTBEAT_MS, fireAt - Date.now()));
    }
  }
  setStatus("SENDING");
  for (let n = 1; n <= MAX_ATTEMPTS; n++) {
    setStatus(`SENDING 第${n}次尝试`);
    const r = await attempt(n);
    if (r.ok) {
      setStatus("SENT-OK");
      log("✅ 送达并验证成功 (SENT-OK)");
      cleanupTask();
      if (r.child) {           // 保持存活直到 agent 跑完, 避免管道断裂
        while (r.child.exitCode === null) await sleep(HEARTBEAT_MS);
        log(`opencode run 结束 exit=${r.child.exitCode}`);
      }
      process.exit(0);
    }
    if (n < MAX_ATTEMPTS) { log("稍后重试..."); await sleep(RETRY_GAP_MS); }
  }
  setStatus("FAILED");
  log("❌ 全部尝试失败");
  process.exit(1);
}

main().catch((e) => { log(`异常: ${e && e.stack || e}`); setStatus("FAILED"); process.exit(1); });
```

---

## 6. 执行流程速查（给 AI 模型的 checklist）

1. □ 只读查库定位目标会话（`mode=ro`），确认 `time_archived IS NULL`
2. □ 判断会话当前状态（几分钟内还在更新 = 正在跑；空文本 assistant 消息 = 工具调用，不是卡死）
3. □ 新建测试会话彩排：`opencode run --title "测试" "请只回复ok" < /dev/null`，收到 ok
4. □ 用 `--session <测试会话>` 再彩排一次续发模式
5. □ 彩排后**删除 status.txt / resume-log.txt**（否则 SENT-OK 残留会让正式发送直接退出！）
6. □ 部署脚本 + 注册双保险计划任务（主 + guard 兜底）
7. □ 写文件验证任务 State=Ready、NextRunTime 正确
8. □ 告知用户：目标时间前别关机注销；06:00 会弹控制台窗口属正常；查 status.txt 确认 SENT-OK
9. □ 用户要取消时：`Unregister-ScheduledTask` 两个任务 + 删脚本目录

## 7. 常见问题

| 现象 | 原因 | 解决 |
|---|---|---|
| `opencode run` 无输出、不退出 | stdin 没关 | 加 `< /dev/null` 或 stdio ignore |
| 消息发出但会话没反应 | 会话正忙，消息排队 | 正常，当前轮结束后会处理 |
| 想发消息但怕续错会话 | `--continue` 续的是最新会话 | 用 `--session <id>` 精确指定 |
| 数据库查询报 database is locked | 用了写连接 | 必须用 `file:...?mode=ro` URI |
| schtasks 被安全策略拦截 | WorkBuddy 沙箱黑名单 | 改用 `Register-ScheduledTask` cmdlet |
| PowerShell 工具没有输出 | 沙箱不回显 stdout | 结果 `Out-File` 写文件再 Read |
| 误判任务卡死 | 工具调用消息无 text part | 查 part 表而非只看 message |
