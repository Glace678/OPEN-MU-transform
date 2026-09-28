# Glace678/3 仓库全量代码审查报告

> 审查日期：2026-09-25
> 审查对象：https://github.com/Glace678/3（main 分支快照，约 42,337 个文件 / 590 万行）
> 审查方式：完整下载仓库 → 区分自研代码与上游代码 → 6 路并行深度扫描 + 主线逐文件精读（根目录全部脚本、.github 全部 workflow、zapier-review 全部 27 个源文件与 6 个测试文件、关键 C++/C#/ETS 疑点现场复核）
>
> **置信度标记**：
> - ✅ = 本人逐行复核过源码，结论有行号证据
> - ⚠️ = 子代理扫描发现，未逐行二次复核（已注明文件与行号，可按图索骥）
> - ❌ = 子代理报告但经我复核**不成立**的发现（见第 9 章，诚实披露）

---

## 目录

1. 仓库结构与审查覆盖范围
2. P0 级严重 Bug（必须立即修复）
3. P1 级高危问题
4. P2 级一般 Bug 与健壮性问题
5. 安全问题专项清单
6. 分模块超详细改进清单
7. CI/CD 与 zapier-review 自动化系统专项
8. 代码质量、架构与工程实践建议
9. 复核后不成立/降级的发现（诚实披露）
10. 修复优先级路线图

---

## 1. 仓库结构与审查覆盖范围

| 目录 | 内容 | 性质 | 覆盖 |
|---|---|---|---|
| `OpenMU/` | C# 游戏服务器（.NET, EF Core, PostgreSQL） | 上游 fork + 自研 Solo 扩展 | 自研部分全查，上游抽查 |
| `MuMain/` | MU Online 客户端（C/C++, SDL3, OpenGL/GLES） | 泄露源码 + 自研移植层 | 自研部分全查 |
| `OpenMU-Android/` | Android 移植（Gradle, Java/Kotlin, NativeAOT） | 自研 | 全查 |
| `OpenMU-HarmonyOS/` | 鸿蒙移植（ArkTS/ETS, hvigor, NAPI） | 自研 | 全查 |
| `OpenMU-iOS/` | iOS 移植（Xcode/CMake, Obj-C++） | 自研 | 全查 |
| `OpenMU-数值重设计/` | C# BalanceLab 数值平衡系统（30,224 断言） | 自研 | 全查 |
| `loc/`、`_locwork/` | 韩文→中文本地化工具链（Python） | 自研 | 全查 |
| `.github/workflows/` | 4 个 GitHub Actions 工作流 | 自研 | 全查 ✅ |
| `.github/zapier-review/` | AI 自动代码审查系统（27 源文件 + 6 测试 + 4 文档） | 自研 | 全查 ✅ |
| 根目录 11 个脚本 | PS1/Python 运维脚本 | 自研 | 全查 ✅ |

---

## 2. P0 级严重 Bug（必须立即修复）

### P0-1 ✅ 鸿蒙 Bootstrap：`Uint8Array.buffer` 直写文件，字节偏移错误 + OOM 风险
**文件**：`OpenMU-HarmonyOS/harmony-game/entry/src/main/ets/pages/Bootstrap.ets:88-95`（`harmony-pc` 同款 `Bootstrap.ets:57` ⚠️）

```typescript
const bytes = await context.resourceManager.getRawFileContent(rawName);
const out = fs.openSync(destPath, ...);
fs.writeSync(out.fd, bytes.buffer);   // ← 三重问题
```

三个独立缺陷叠加：
1. **`bytes.buffer` 忽略 `byteOffset`/`byteLength`**。`getRawFileContent` 返回的 `Uint8Array` 可能是共享 ArrayBuffer 上的视图；直接写 `.buffer` 会把视图之外的内存一并写入（或写错起点），产出的 `game-data.zip` 是损坏的 → 解压失败或静默数据损坏。
2. **`writeSync` 无部分写循环**。单次 `writeSync` 允许返回小于请求的写入字节数，大文件（游戏数据包约 2GB）几乎必然触发部分写，剩余字节被静默丢弃。
3. **整个 zip 一次性读入内存**。`getRawFileContent` 把 ~2GB 的 rawfile 全部载入 JS 堆，中低端手机直接 OOM 崩溃。

**修复**：改用 `resourceManager.getRawFile`（返回 fd）+ 分块 `fs.copyFile`/循环 `read/write`；若必须用 Uint8Array，写入 `bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)` 并循环直到写完。

### P0-2 ⚠️ 服务器启动路径同步阻塞异步 → 死锁风险
**文件**：`OpenMU/Startup/Program.cs:191,290,319,320,368,384,394,404,424`

9 处 `.WaitAndUnwrapException()` 在同步上下文中阻塞等待异步任务。在 ASP.NET Core 线程池耗尽或存在同步上下文时会产生经典 async deadlock，服务器启动挂死且无异常输出。
**修复**：将调用链改为 `async Task` 一路 `await` 到 `Main`；至少对启动路径消除阻塞等待。

### P0-3 ⚠️ 本地化工具 `exec()` 执行任意代码
**文件**：`_locwork/run_translate.py:18-21`

对（可能来自外部翻译产物的）字符串直接 `exec()`。任何能影响翻译中间文件的人/进程都能在你的机器上执行任意 Python。
**修复**：改为 `ast.literal_eval` 或结构化 JSON 数据交换，绝不对数据执行 `exec/eval`。

### P0-4 ⚠️ 本地化管线就地覆写源文件、无备份
**文件**：`loc/bmd.py:140`、`loc/apply_translations.py:123` 等

BMD 解密→翻译→回写全程 in-place，任何一步异常（编码错误、磁盘满、Ctrl+C）都会把唯一的游戏资源原件变成半成品。
**修复**：写入 `*.tmp` 后 `os.replace` 原子替换；处理前生成 `.bak` 或输出到独立目录。

### P0-5 ✅ `provision_account.ps1`：`ON CONFLICT` 目标列错误，脚本不可重入
**文件**：`provision_account.ps1`（根目录）

```sql
INSERT INTO "Account" (...) VALUES (...) ON CONFLICT ("Id") DO NOTHING
```
`Id` 是每次新生成的 GUID，永远不会冲突；真正会冲突的是 `LoginName` 唯一约束。第二次运行（例如重装/换机重配）直接抛唯一约束异常，provision 流程中断。
**修复**：`ON CONFLICT ("LoginName") DO NOTHING`（或 `DO UPDATE`）。

### P0-6 ✅ `packtool.py`：解压路径穿越 + 记录定位假设脆弱
**文件**：`packtool.py`（根目录，246 行）

1. **路径穿越**：`cmd_extract` 的 `NAME_RE` 允许文件名包含 `../`，恶意/损坏的 exe 内嵌包可以把文件写到解压目录之外（覆盖任意可写文件）。修复：`os.path.realpath` 校验目标必须位于输出目录内，或直接拒绝含 `..`/绝对路径的条目名。
2. **固定偏移假设**：从 `pos = len(data) - 24` 开始解析，隐含"最后一条记录的文件名恰好 22 字节"。名字长度不同则整个解析静默错位，产出垃圾而不报错。修复：从文件头正向遍历记录链，或校验魔数/长度自洽后再解析。
3. **文档与代码不一致**：docstring 写 "name-len:varint"，代码按单字节读取。二者必须统一（若真实格式是 varint，当前代码对 >127 字节名字全部解析错误）。

### P0-7 ⚠️ Android 打包脚本硬编码 build-tools 版本
**文件**：`OpenMU-Android/Build-AndroidPackage.ps1:271`

```powershell
...build-tools\36.0.0\apksigner.bat
```
SDK 管理器装的是其他版本时构建直接失败，且报错信息不指向根因。
**修复**：枚举 `$env:ANDROID_HOME/build-tools` 取最高版本，或从 `source.properties` 解析。

---

## 3. P1 级高危问题

### 服务器（OpenMU）
| # | 问题 | 位置 | 置信 |
|---|---|---|---|
| P1-1 | 公开注册端点无速率限制，可被脚本批量注册 | `PublicRegistrationEndpoints.cs:31` | ⚠️ |
| P1-2 | `MobileGmService._grantOperations` 字典只增不减 → 长期运行内存泄漏 | MobileGmService | ⚠️ |
| P1-3 | `LocalStackManager` 清理路径传 `CancellationToken.None`，关机时清理任务无法被取消，拖慢/挂住退出 | LocalStackManager | ⚠️ |
| P1-4 | `Simulation.cs` 存在除零路径（partySize/denominator 为 0 时） | 数值重设计 Simulation.cs | ⚠️ |
| P1-5 | IP 白名单在反向代理后取 `RemoteIpAddress`，可被 `X-Forwarded-For` 伪造绕过（需配合 ForwardedHeaders 中间件白名单） | 服务器网络层 | ⚠️ |
| P1-6 | 启用移动端时绑定 `0.0.0.0`，局域网内任意设备可直连游戏/GM 端口 | 服务器配置 | ⚠️ |
| P1-7 ✅ | `SoloCashShopService.CommitAsync` 回滚路径中 `RemoveItemAsync` 若抛异常，异常向上传播且此时 `SoloCashShopData` 已回滚、物品却可能仍在背包 → 极端情况下道具复制；另 `SaveProgressAsync` 返回 `false`（非异常）时静默回滚、零日志 | `SoloCashShopService.cs:176-204` | ✅ |

### 客户端（MuMain）
| # | 问题 | 位置 | 置信 |
|---|---|---|---|
| P1-8 | 非 Windows 平台用 `SDL_setenv_unsafe` 把游戏账号密码写入进程环境 → Linux/Android 上任何同 uid 进程可读 `/proc/<pid>/environ` | `LocalLoginCredentials.cpp:91` | ✅ |
| P1-9 | 静态字体缓存无淘汰机制，长时间运行（切换大量字体/字号）内存只增不减 | `GdiText.cpp` | ⚠️ |
| P1-10 | DPAPI 解密后的明文凭证驻留内存，未在用完后清零（`SecureZeroMemory`） | local-credentials 链路 | ⚠️ |
| P1-11 | BMD XOR 解密逻辑在 `loc/bmd.py` 与 `loc/formats.py` 各实现一份，已经出现行为分叉的土壤 | loc/ | ⚠️ |

### 移动端
| # | 问题 | 位置 | 置信 |
|---|---|---|---|
| P1-12 | Android 首次启动把 740MB zip 解压两遍（assets→filesDir→dataRoot 各一次完整拷贝），首启时间和磁盘峰值翻倍 | Android Bootstrap | ⚠️ |
| P1-13 | GM 辅助 App 数组越界崩溃（选中角色数 < 预期时） | GM app | ⚠️ |
| P1-14 | 鸿蒙 `display` 事件监听器注册后未在 `aboutToDisappear` 注销 → 页面反复进出泄漏回调 | harmony Game 页 | ⚠️ |
| P1-15 | 防火墙脚本 release 版缺少网络类型检查（Domain/Private/Public 一刀切放行） | 防火墙 ps1 | ⚠️ |

### 自动化系统（zapier-review / workflows）
| # | 问题 | 位置 | 置信 |
|---|---|---|---|
| P1-16 | `provider-review.yml` 预推理校验只跑 5 个 guard 测试中的 3 个，**漏掉 `usage-guard.test.mjs` 和 `autonomous-core.test.mjs`** —— 恰好是"防超支"和"防配置篡改"两道最关键的闸门 | `.github/workflows/provider-review.yml` | ✅ |
| P1-17 | `provider-review.yml` 默认 token 预算 100,000,000（1 亿），一次误触发即可产生巨额 API 账单；对比 `luna-autonomous.yml` 的 74 tasks 上限，防护等级严重不对称 | 同上 | ✅ |
| P1-18 | `validation-runner.mjs` 文件锁缺陷：`writeFileSync(lock,...,{flag:'wx'})` 因锁已存在（EEXIST）抛错时，`finally{unlinkSync(lock)}` 会**删掉另一个正在运行进程持有的锁** → 互斥失效，可能双进程同时提交模型请求（正是该系统设计上最禁止的事） | `validation-runner.mjs:26,66` | ✅ |

---

## 4. P2 级一般 Bug 与健壮性问题

### 根目录脚本（全部 ✅ 本人复核）

| # | 文件 | 问题 |
|---|---|---|
| P2-1 | `patch_manifest.py` | `norm(p)` 用 `p.lstrip('./')` —— `lstrip` 按**字符集合**剥离，`'./Data/x.bmd'` 会变成 `'Data/x.bmd'` 但 `'..foo'` 变成 `'foo'`，任何以 `.` 开头的合法目录名都被啃掉。应改 `removeprefix('./')` 或 `os.path.normpath` |
| P2-2 | `patch_manifest.py` | `open(full,'rb').read()` 文件句柄不关闭（CPython 引用计数兜底，但 PyPy/Windows 共享模式下会出问题） |
| P2-3 | `patch_manifest.py` | 非原子写 manifest：写到一半崩溃 → JSON 损坏且无备份 |
| P2-4 | `patch_manifest.py` | `sample = files[0]` 空列表时 `IndexError`；`MANIFEST` 变量定义后从未使用（死代码） |
| P2-5 | `_migrate_paths.py` | `'W' in expr` 子串判断误报：`'config.WORKSPACE'` 含字母 `W` → `uses_W` 被错误置 True，迁移逻辑走错分支。应用 `re.search(r'\bW\b', expr)` |
| P2-6 | `_migrate_paths.py` | `rewrite_source()` 的 `own_subdir` 参数从未使用；脚本内硬编码原机器 Windows 字面路径做比较，换机即失效 |
| P2-7 | `wakeup_claude.py` | PowerShell 转义错误：在单引号字符串里 `.replace('"', '`"')` 产生的是字面反引号+引号，不是 PS 转义序列 → 含双引号的参数传给 claude CLI 时语法损坏 |
| P2-8 | `wakeup_claude.py` | docstring 写"凌晨 2:00"，默认值实为 4:00；硬编码 session id；`subprocess.CREATE_NEW_CONSOLE` 仅 Windows 可用（无平台守卫）；使用 `--dangerously-skip-permissions` 却无审计日志 |
| P2-9 | `provision_secrets.ps1` | `AdminPassword` 走命令行参数 → 出现在进程列表与 PowerShell 历史；`GAME_USER`/`GAME_PASS` 明文 echo 到 stdout（被 CI/终端日志捕获）；`$rng` 未 `Dispose()`。应改 `Read-Host -AsSecureString` + `[Runtime.InteropServices.Marshal]::SecureStringToBSTR` 用后清零 |
| P2-10 | `local-credentials.ps1` | HMAC 派生凭证熵有限（用户名 = `solo`+6 hex，密码 = 20 base64 字符 ≈ 120bit，尚可），但 DPAPI entropy 字符串 `"OpenMU-Local.Secrets.v1"` 硬编码在公开仓库 → 任何拿到 `local-secrets.dpapi` 文件副本的人可在同机同用户下解密。单机场景可接受，需在 README 明示威胁模型 |
| P2-11 | `start_client.ps1` | `$psi.EnvironmentVariables` 赋给 `$env` 风格变量名易与 PowerShell 自动变量 `$env:` 混淆；无错误处理 |
| P2-12 | `make_shortcut.ps1` | COM 对象（WScript.Shell）未 `ReleaseComObject`；全程无 try/catch，目标路径不存在时抛裸异常 |
| P2-13 | `config.py` | `_ServerProxy.__getattr__` 每次属性访问都新建 `_Paths` 实例，无缓存；代理吞掉 `AttributeError` 导致拼写错误延迟到运行深处才暴露 |
| P2-14 | `dumpenv.ps1` | 读取任意进程 PEB 环境变量的双用途工具，本身实现无 bug（WOW64 限制已文档化），但建议文件头加"仅限本机调试、可能触发 EDR 告警"警示，避免被误当生产工具 |

### zapier-review（全部 ✅ 本人复核）

| # | 文件 | 问题 |
|---|---|---|
| P2-15 | `package.json` | `npm test` 只跑 `worker.test.mjs`，而 workflow 跑 5 个测试文件 —— 本地"测试全绿"与 CI 校验范围不一致，容易漏改。应改 `node --test *.test.mjs` |
| P2-16 | `validation-runner.mjs:63` | catch 块里 `read(file)` 若账本不存在会再抛 ENOENT，**掩盖原始错误**；应先 `existsSync` 或用 try 包裹 |
| P2-17 | `zap-dispatch.js:26` | `connections?.github \|\| 66435003` 硬编码 Zapier App Connection ID 兜底 —— 换账号后静默用错连接；应显式报错 |
| P2-18 | `prepare-full-audit.mjs:30 vs 48` | 分段构建用固定 headroom `target-90000`，装箱却用 `target-promptReserve`（=14000+policy tokens）。policy 很长时 promptReserve>90000，单个"合格"分段反而装不进任何 bin → 直接 throw `Oversize dictionary segment`，整个审计准备失败。两处 headroom 应统一为 `max(90000, promptReserve)` |
| P2-19 | `prepare-full-audit.mjs:61` | 无 job 时读 `expected-quality.json` 并 `filter((_,i)=>[0,5,6].includes(i))` —— 隐含 fixture 文件至少 7 条；少于 7 条时 `controlEntries` 含 undefined，后续 `f.text` 崩溃且报错信息不指向根因 |
| P2-20 | `prepare-full-audit.mjs:88` | 覆盖率校验对每个文件做 `segments.filter(...)`，O(files×segments) 二次方复杂度；18,877 个文本文件时明显变慢，可先按 file 分组一次 |
| P2-21 | `prepare-full-audit.mjs` | 全部唯一块字典（`blocks` Map）驻留内存；按 COST-RESEARCH 数据全仓唯一块 ≈ 6170 万 tokens 的原文，内存占用可达数 GB，无落盘/流式路径 |
| P2-22 | `assess-probe.mjs:13,33` | 两处 `catch{}` 静默吞错：transport 校验失败时 `transportChecksValid=false` 但无原因字段；`needs_context_json` 解析失败无痕。至少记录 `error.message` |
| P2-23 | `billing-probe.mjs:50` | `newInvocations: resume?0:1` —— `execute` 模式下若发现已有 runId 的 state（第 43 行跳过创建），仍报 1 次新调用，统计失真 |
| P2-24 | `usage-guard.mjs` | 账户 ID `'28766408'` 硬编码；换账号时 guard 静默比对错误账户的用量。应从环境变量读取并在缺失时 fail-closed |
| P2-25 | `compare-packing.mjs:43` | `Math.max(...bins.map(...))` 展开大数组有调用栈上限风险（当前 ~106 bins 安全，参数调小 chunk 后可能上千）；改 `reduce` |
| P2-26 | `measure-compression.mjs:17` | `blocks()` 中 `text.lastIndexOf('\n', end-1)` 在 `end=0` 时 fromIndex=-1，行为依赖引擎 clamp 语义（结果无害但属未明边界）；`block-index.json` 全量落盘可达数十 MB 无提示 |
| P2-27 | `compressed-transport.mjs` | 对请求体设置 `content-encoding: gzip` 属实验性服务端支持，失败时无降级重试路径（注释已声明实验性质，建议加显式开关与失败回退） |
| P2-28 | `result-store.mjs:23` | 128 段 × 8000 字符 = 1MB 结果上限，超限直接 throw 且提示"扩容后恢复"——设计如此，但错误信息未包含当前实际段数，排障时要自己算 |

### 本地化 / 数值 / 其他（⚠️ 子代理扫描）

| # | 位置 | 问题 |
|---|---|---|
| P2-29 | `loc/` 30+ Python 脚本 | 普遍 `open()` 不用 with/不关闭句柄；批处理大 BMD 时 Windows 下句柄耗尽风险 |
| P2-30 | `loc/` 编码处理 | EUC-KR/CP949/UTF-8 三套编码路径散落各脚本，无统一 codec 层，`errors='replace'` 静默丢字符处 |
| P2-31 | GM app | 输入校验缺失（等级/点数输入非数字时崩溃而非提示）⚠️ |
| P2-32 | 鸿蒙 `Bootstrap.ets:172-177` ✅ | 设置页 `serverAddress`、`fps` 为自由文本输入，无格式校验（fps 声称只接受 follow/30/45/60/90/120 但任意字符串都能存进配置，错误延迟到原生层才暴露） |
| P2-33 | 鸿蒙 `Bootstrap.ets:29-41` ✅ | `aboutToAppear` 内 async 调用无 try/catch，`MobileConfig.getString` 抛错 → 未处理 Promise rejection，界面永远停在"准备游戏数据…" |

---

## 5. 安全问题专项清单

按风险排序（综合置信度与影响面）：

1. **【高】公开仓库托管 MU Online 客户端源码（`MuMain/`）** —— Webzen 拥有版权的泄露代码放在 public repo，存在 DMCA takedown 与法律风险；整个仓库随时可能被封，连带所有自研工作丢失。**建议**：至少将 `MuMain` 拆到私有仓库，公开仓库只保留自研移植层 patch。
2. **【高】`provision_secrets.ps1` 密码走命令行 + 明文回显**（P2-9）—— 进程列表、PSReadLine 历史、CI 日志三处泄漏面。
3. **【高】`run_translate.py` 的 `exec()`**（P0-3）—— 数据文件即代码执行入口。
4. **【高】`packtool.py` 解压路径穿越**（P0-6）。
5. **【中】公开注册端点无速率限制**（P1-1）+ **IP 白名单可被代理头绕过**（P1-5）+ **移动端启用时 0.0.0.0 绑定**（P1-6）—— 三者叠加：公网可达时任何人可注册账号并绕过白名单打 GM 接口。
6. **【中】GM App `network_security_config.xml` 全局允许 cleartext**（⚠️）—— 应限定到局域网 debug 构建。
7. **【中】配对密钥（pairing key）经 HTTP 头明文传输 + `build-profile.json5` 占位密钥硬编码**（⚠️）—— 局域网嗅探即可劫持移动端↔服务器配对。
8. **【中】非 Windows 平台凭证明文进环境变量**（P1-8）。
9. **【中】DPAPI entropy 硬编码公开**（P2-10）—— 威胁模型限于"同机同用户"，可接受但必须文档化。
10. **【低】`luna-review.yml` 硬编码 `ZAPIER_TABLE_ID='01M36XPZ65N821F3AN8FZCW045'`、`zap-dispatch.js` 硬编码 connection ID、`usage-guard.mjs` 硬编码账户 ID** —— 信息暴露 + 换环境静默失效，应全部转 secrets/环境变量。
11. **【低】`wakeup_claude.py` 使用 `--dangerously-skip-permissions` 且硬编码 session** —— 无人值守 + 跳过权限确认的组合，若 session 内容被污染等于给了任意命令执行通道；建议至少加操作审计日志与允许命令白名单。
12. **【低】`GdiText.cpp:102` `popen` 调用 `fc-match`** ✅ 复核：family 名的单引号已被剥除（97-99 行），shell 注入基本被堵住；但 `popen` 走 `/bin/sh` 仍是次优选择，建议 `posix_spawn`/`execvp` 传参数组彻底绕开 shell，同时消除对 `2>/dev/null` 语法的依赖。
13. **【良好实践，值得肯定】** ✅ `.gitignore` 正确排除 `.env`、`*.dpapi`、`*.p12/pfx/jks/keystore`、`mobile-server-settings.json`；`luna-autonomous.yml` 有 owner 校验、分支名正则、快照固定；`file-operations.mjs` 的 `safePath` 拒绝 `../`、绝对路径、`.GIT` 大小写绕过、盘符路径，且测试覆盖（`file-operations.test.mjs:33`）；`verify-probe-attachments.mjs` 限定 raw.githubusercontent + commit 固定 + `redirect:'error'` + SHA256 校验。

---

## 6. 分模块超详细改进清单

### 6.1 OpenMU 服务器（C#）
1. 启动路径全面 async 化，消灭 `WaitAndUnwrapException`（P0-2）。
2. `SoloCashShopService.CommitAsync`：回滚中的 `RemoveItemAsync` 包 try/catch 并记录"回滚不完整"致命日志；`SaveProgressAsync` 返回 false 时补一条 Warning（当前只有异常路径有日志）。
3. `MobileGmService._grantOperations` 加 TTL 或完成即删（P1-2）。
4. 注册端点加 IP+账号名双维度速率限制（如 ASP.NET Core Rate Limiting 中间件，fixed window 足够）。
5. 白名单改信 `ForwardedHeadersMiddleware` 且 `KnownProxies/KnownNetworks` 显式配置。
6. 移动端监听地址做成配置项，默认回环。
7. Solo 扩展与上游 fork 的 diff 建议维护成 patch 系列（quilt/git format-patch），上游更新时冲突面可控。

### 6.2 MuMain 客户端（C/C++）
1. 凭证传递改平台安全存储（Linux: libsecret；Android: Keystore），至少不要 `setenv`（P1-8）。
2. DPAPI 解密缓冲用后 `SecureZeroMemory`。
3. 字体缓存加 LRU 上限（P1-9）。
4. `popen` → `posix_spawn`（见安全清单 #12）。
5. BMD XOR 解密收敛到单一实现（与 loc 工具共享规范文档，注明块大小/密钥表版本）。

### 6.3 Android
1. build-tools 版本自动探测（P0-7）。
2. 首启解压去重：assets zip 直接流式解压到最终 dataRoot，删掉中间拷贝（P1-12，省 ~740MB 磁盘峰值和一半首启时间）。
3. GM app：输入校验 + 数组边界检查（P1-13/P2-31）+ cleartext 仅限 debug。

### 6.4 HarmonyOS
1. Bootstrap 文件写入三缺陷修复（P0-1）——这是鸿蒙端首启失败的最可能根因。
2. `aboutToAppear` 加 try/catch 并把错误渲染到 `status/detail`（P2-33）。
3. display 监听器成对注销（P1-14）。
4. 设置页输入校验：fps 用枚举选择器（Select）替代自由文本；serverAddress 用 IP 正则（P2-32）。
5. `harmony-game` 与 `harmony-pc` 的 Bootstrap 高度重复，抽公共模块，避免 P0-1 这类 bug 修一处漏一处。

### 6.5 iOS
1. 脚手架已按审计报告补齐；建议把 CMake 工具链文件与 Android/HarmonyOS 的 native 构建参数（宏定义、源文件清单）做成单一事实来源（如共享 .cmake 或生成器），三端漂移是长期隐患。

### 6.6 本地化工具链（loc/、_locwork/）
1. 删除 `exec()`（P0-3）。
2. 全部改写为"临时文件 + 原子替换 + 可选备份"（P0-4）。
3. 统一句柄管理（with 语句）与编码层（单一 `decode_bmd_text/encode_bmd_text` 模块，显式 errors 策略，禁止静默 replace）。
4. `bmd.py` 与 `formats.py` 合并去重（P1-11）。
5. 建议补一个"翻译往返校验"脚本：apply 后重新解密比对未翻译区域字节级一致。

### 6.7 数值重设计（BalanceLab）
1. `Simulation.cs` 除零守卫（P1-4）。
2. `Rules.cs` 最大余数法 ✅ 复核安全（见第 9 章），但建议加一行注释说明 `points - allocated.Sum() ∈ [0,4]` 的不变量，防止未来改动破坏前提。
3. `Verify.ps1` ✅ 复核整体健壮（前后哈希对比、原子 manifest、只读探针），是仓库里质量最高的脚本之一；小改进：`design\*.json` 不存在时报错信息可更明确。

### 6.8 根目录脚本
见 P2-1 ~ P2-14 逐条。另建议：
- 所有 PS1 统一 `#Requires -Version 5.1`、`Set-StrictMode -Version Latest`、try/finally 释放 COM/RNG。
- 所有 Python 脚本统一 `pathlib` + context manager，删掉 `_migrate_paths.py` 这类一次性迁移脚本或移入 `scripts/archive/` 明示已废弃。

---

## 7. CI/CD 与 zapier-review 自动化系统专项

### 7.1 Workflows
| 文件 | 评价 |
|---|---|
| `luna-autonomous.yml` ✅ | 质量高：owner 校验、分支正则、快照固定、5 个 guard 测试全跑。无问题 |
| `luna-review.yml` ✅ | job 级 + 脚本级双重 owner 检查，好；硬编码 TABLE_ID 应转 secret |
| `provider-review.yml` ✅ | **两个实质缺陷**：guard 测试只跑 3/5（P1-16）；默认 1 亿 token 预算（P1-17）。修复：补齐 `node --test usage-guard.test.mjs autonomous-core.test.mjs`；预算默认值降到与 luna 路径同量级并要求显式 override |
| `gm-probe.yml` / `gm_probe.py` ✅ | 探针脚本无实质问题；`os.environ['GH_TOKEN']` 缺失时 KeyError，改 `sys.exit('GH_TOKEN required')` 更友好 |

### 7.2 zapier-review 系统整体评价
这套系统的**安全设计密度远超一般个人项目**，以下机制经我逐行复核确认有效：
- 提交状态不确定时永久占用预算、绝不重发（`validation-runner.mjs:38`、`billing-probe.mjs:33`）；
- SDK 执行 ID 先持久化再轮询，崩溃可恢复同一执行（durable job state）；
- 结果分段存储带 SHA256 完整性链（`result-store.mjs`），代理对（surrogate pair）切分正确处理；
- 传输日志只记 host/path/status/runId，不落凭证与正文（`request-journal.mjs`）；
- 附件预检：URL 白名单正则 + commit 固定 + 15 分钟时效 + 字节级哈希比对（`verify-probe-attachments.mjs` + `attachment-preflight.mjs`）；
- `safePath` 路径穿越防护带测试覆盖；
- 覆盖回执用 manifest SHA256 + 连续区间，避免表字段超限，且明确声明"覆盖是模型自述，不是缺陷完备性证明"——文档诚实度罕见地高。

需要修复的集中在：锁互斥缺陷（P1-18）、本地/CI 测试范围不一致（P2-15）、硬编码标识符（P2-17/24）、`prepare-full-audit` 的 headroom 不一致与 fixture 数量假设（P2-18/19）、静默 catch（P2-22）。

### 7.3 文档
`README.md`、`AUTONOMOUS.md`、`VALIDATION.md`、`COST-RESEARCH.md` ✅ 全部读过：费用结论的更正记录（"expectedZapierTasks: 0 是推算不是实测"）、002 不确定状态永不重发的处理、逐次账本+反思表格，是教科书级的验证纪律。唯一建议：README 顶部加一张"当前开关状态"表（LUNA_ENABLED=false 等），避免读者漏看正文中的停用声明。

---

## 8. 代码质量、架构与工程实践建议

1. **测试不均衡**：zapier-review 有 6 个测试文件且断言扎实；但 loc/ 工具链、根目录脚本、移动端 Bootstrap 零测试。优先给"会覆写用户数据"的 loc 管线补往返测试。
2. **三端移动端代码重复**：Android/HarmonyOS(harmony-game 与 harmony-pc)/iOS 的 Bootstrap、配置读写、手势层各自维护，P0-1 同款 bug 在两处出现即是证据。建议抽"移动端引导流程"共享规范文档 + 各端最小实现清单。
3. **一次性脚本未归档**：`_migrate_paths.py`、`wakeup_claude.py` 等带硬编码路径/session 的脚本混在根目录，建议移入 `scripts/`（可复用）与 `scripts/archive/`（一次性）两级。
4. **文档-代码漂移**：packtool docstring（varint vs 单字节）、wakeup_claude（2:00 vs 4:00）两处已确认漂移。建议 docstring 里的数值/格式声明改为从代码常量生成，或加测试锁定。
5. **错误处理哲学统一**：zapier-review 的 fail-closed（不确定即停止）非常好，但 loc 工具链是 fail-silent（errors='replace'、无备份覆写）。建议把 fail-closed 推广为全仓规范。
6. **依赖固定**：`@zapier/zapier-sdk 0.112.3` 等 beta 依赖建议在 CI 中校验 lockfile 哈希（`npm ci` 已做，保持）；beta API 变更时 guard 测试是第一道警报，务必保持 5/5 全跑。

---

## 9. 复核后不成立/降级的发现（诚实披露）

子代理扫描报告了以下问题，我逐行复核后**推翻或大幅降级**，列出以免误导修复方向：

| 原报告 | 复核结论 |
|---|---|
| ❌ "Rules.cs:46 最大余数法索引越界（P0）" | **不成立**。`Take(points - allocated.Sum())` 作用于 `Range(0,5)` 的 5 元素序列；余数 r 数学上 ∈ [0,4]（分配分数和为 1±1e-9，floor 之和 ≤ points），且 `Enumerable.Take` 对负数返回空序列不抛异常。`allocated[i]` 的 i 恒在 0..4。无越界路径 |
| ❌ "Verify.ps1 `$dotnet.Source` 未定义" | **不成立**。第 7 行 `$dotnet = Get-Command dotnet` 返回 CommandInfo，`.Source` 是标准属性（可执行文件全路径），`& $dotnet.Source` 是合法调用 |
| ⬇️ "GdiText.cpp:567 popen shell 注入（P0 安全）" | **降级为低危建议**。实际位置是 102 行；97-99 行已剥除 family 名中的单引号，值被包裹在单引号内无法逃逸。残余风险仅剩 popen 走 shell 的架构问题 |
| ⬇️ "LocalLoginCredentials.cpp:142 MU_CONFIG_FILE 路径穿越（P0）" | **降级为低危**。142 行实际是凭证环境变量读取；sidecar 机制（55-76 行）确实用 `MU_CONFIG_FILE` 拼路径，但：能设置该环境变量者已拥有进程控制权（不构成提权）；sidecar 文件能设置的变量被 5 项白名单限制（22-28 行）；凭证解析有严格字符集校验（150-166 行）。属本地攻击面，非远程漏洞 |
| ⬇️ "SoloCashShopService 事务回滚不完整（P1）" | **降级为 P2 边缘情况**。主回滚路径（数据、金币、道具、Detach）完整；仅"回滚中 RemoveItemAsync 自身抛异常"这一嵌套失败场景会留下不一致，以及 SaveProgressAsync 返回 false 时无日志 |

---

## 10. 修复优先级路线图

**第一批（数据安全与可用性，建议立即）**
1. P0-1 鸿蒙 Bootstrap 文件写入（首启必坏）
2. P0-5 provision_account ON CONFLICT（重装必炸）
3. P0-3 / P0-4 loc 工具链 exec() 与无备份覆写（原件不可再生）
4. P0-6 packtool 路径穿越 + 解析假设
5. P1-18 validation-runner 锁缺陷（双提交 = 重复计费，系统自身最禁止的事）

**第二批（安全加固）**
6. P2-9 provision_secrets 密码传递方式
7. P1-1/P1-5/P1-6 注册限速 + 代理头 + 绑定地址
8. P1-8 非 Windows 凭证环境变量
9. P1-16/P1-17 provider-review.yml 补齐 guard 测试 + 降默认预算
10. MuMain 拆私有仓库（法律风险，越早越好）

**第三批（稳定性）**
11. P0-2 服务器启动 async 化
12. P0-7 Android build-tools 探测
13. P1-2/P1-3/P1-4/P1-12/P1-13/P1-14
14. P2-18/19 prepare-full-audit headroom 与 fixture 假设

**第四批（卫生与一致性）**
15. P2 清单其余各项（句柄、原子写、文档漂移、硬编码 ID、静默 catch）
16. 第 8 章架构建议按需排期

---

### 统计汇总

| 级别 | 数量 |
|---|---|
| P0 严重 bug | 7 |
| P1 高危 | 18 |
| P2 一般 bug/健壮性 | 33 |
| 安全专项 | 13 项（含 1 项法律风险、4 项良好实践确认） |
| 复核推翻/降级的子代理误报 | 5 |
| 逐文件精读并给出改进点的文件 | 60+（根脚本 11、workflow 5、zapier-review 33、关键 C++/C#/ETS/PS1 疑点 12+） |

> 说明：⚠️ 标记项来自并行深度扫描，已给出文件与行号线索但未逐行二次确认；修复前建议先按图核对现场。✅ 标记项均有本人读取的源码行号证据。
