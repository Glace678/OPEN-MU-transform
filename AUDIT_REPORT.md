# OpenMU workspace - read-only code audit report

- Generated: 2026-09-26T15:49:34.502Z
- Model: `openai/gpt-5.6-luna` via AI by Zapier (AICLIAPI.get_completion), standard tier (~1 task/call)
- Mode: **READ-ONLY review - the audit only read files; no code was modified or patched**
- Task budget: UI baseline **34/1000**; new model dispatches **13**; projected final **~47/1000**; hard ceiling **50** (NOT exceeded)
- Batches dispatched/completed: 13 / 13 of 13
- Scope: 60 user-authored glue/ops/automation files, 276.3 KB, 13 batches
- Excluded: third-party engines OpenMU/MuMain, mobile/HarmonyOS/iOS folders, localization/generated data, node_modules, build output

## Findings overview

| critical | high | medium | low | info | total |
|---|---|---|---|---|---|
| 0 | 9 | 38 | 18 | 0 | 65 |

**By category:** race-condition (6), resource-exhaustion (6), error-handling (5), arbitrary-file-write (3), missing-error-handling (3), encoding (3), input-validation (3), sensitive-data-exposure (3), command-injection (2), prompt-injection (2), unsafe-file-write (2), credential-exposure (2), secrets-in-logs (2), encoding-bug (2), ci-automation (2), arbitrary file write (1), code-execution (1), path-traversal (1), unsafe-agent-execution (1), logic-error (1), validation (1), data-exposure (1), resource-management (1), reliability (1), denial-of-service (1), authorization (1), secret-exposure (1), authentication (1), information-disclosure (1), transport-security (1), automation (1), missing-timeout (1), automation-error-handling (1), automation-bug (1)

## Executive summary (auto-generated from findings; zero extra model calls)

The read-only review of 60 user-authored files returned 65 finding(s): 0 critical, 9 high, 38 medium, 18 low, 0 info.

Fix first (9 critical/high):
- **[high]** 补丁创建和目标路径缺少范围校验 - `.github/zapier-review/autonomous-core.mjs:61`
- **[high]** 以完整进程环境执行仓库内脚本 - `.github/zapier-review/autonomous.mjs:59`
- **[high]** 排队状态缺少原子认领，可能重复提交模型任务 - `.github/zapier-review/native-sdk.mjs:56`
- **[high]** 令牌预算可被无效输入绕过 - `.github/zapier-review/provider-autonomous.mjs:41`
- **[high]** 创建补丁未限制在已审核的源码范围内 - `.github/zapier-review/provider-autonomous.mjs:445`
- **[high]** 输出目录可被符号链接劫持 - `.github/zapier-review/worker.mjs`
- **[high]** 解包文件名可逃逸输出目录并覆盖任意文件 - `packtool.py:172`
- **[high]** 以跳过权限确认模式运行自动化代理 - `wakeup_claude.py:126`
- **[high]** 工作目录未经转义拼接进 PowerShell 命令 - `wakeup_claude.py:143`

All items below are model-generated and must be verified by a human before acting. No source file was modified by this audit.

## Detailed findings (ordered by severity)

### HIGH (9)

#### [HIGH] 补丁创建和目标路径缺少范围校验

- **Location:** `.github/zapier-review/autonomous-core.mjs:61`
- **Category:** arbitrary file write
- **Detail:** 校验条件对 op 为 create 的 edit.path 直接放行，仅排除 validation-fixtures/；非 create 操作也只校验 edit.path，未限制 edit.to。绝对路径或包含 ../ 的路径因此可能通过校验，后续补丁应用若按路径写入或重命名，可能越出仓库目录并修改任意文件。
- **Suggestion (analysis only, NOT applied):** 对所有 edit.path 和 edit.to 统一执行路径校验：必须是仓库内的相对路径，拒绝绝对路径、\、NUL 和 .、.. 路径段，并在规范化后确认仍位于允许的源目录；对 create 操作也限制可创建的目录，并在应用阶段防止符号链接绕过。

#### [HIGH] 以完整进程环境执行仓库内脚本

- **Location:** `.github/zapier-review/autonomous.mjs:59`
- **Category:** code-execution
- **Detail:** prepare-full-audit.mjs 来自当前检出的仓库，并通过继承完整 process.env 的子进程执行，因此可获得 GITHUB_TOKEN、Zapier 客户端凭据等敏感环境变量。若任务运行在不受信任的提交或该辅助脚本被篡改，脚本可执行任意操作并外泄凭据。
- **Suggestion (analysis only, NOT applied):** 只执行固定版本、受信任位置的辅助程序，并通过显式白名单传递最小环境变量；不要向仓库内脚本传递 GitHub 或 Zapier 凭据，并限制任务令牌权限。

#### [HIGH] 排队状态缺少原子认领，可能重复提交模型任务

- **Location:** `.github/zapier-review/native-sdk.mjs:56`
- **Category:** race-condition
- **Detail:** 多个并发执行者都可能读取到 queued，随后分别通过免费检查并调用 createActionRun。save 不是条件更新或锁，最终可能覆盖其中一个 run ID，造成重复模型调用、重复扣费以及无法恢复的执行记录。
- **Suggestion (analysis only, NOT applied):** 使用持久化锁或带版本条件的原子 queued→starting 更新，只有成功认领者才能创建任务；为创建操作增加幂等键并保留唯一的执行 ID。

#### [HIGH] 令牌预算可被无效输入绕过

- **Location:** `.github/zapier-review/provider-autonomous.mjs:41`
- **Category:** resource-exhaustion
- **Detail:** tokenBudget 使用 Number(input.max_tasks \|\| 100000000)。非数字值会得到 NaN，使 total > tokenBudget 永远为假；0 也会被替换成默认的 100000000。字段名还与令牌预算语义不一致，可能导致意外的大量 provider 请求和费用。
- **Suggestion (analysis only, NOT applied):** 使用独立的 token_budget 输入，要求其为有限的正整数并设置服务端硬上限；无效或缺失时应拒绝运行，而不是静默采用超大默认值。每次响应更新用量后也应立即执行预算检查，阻止后续请求。

#### [HIGH] 创建补丁未限制在已审核的源码范围内

- **Location:** `.github/zapier-review/provider-autonomous.mjs:445`
- **Category:** arbitrary-file-write
- **Detail:** 校验条件对 edit.op 为 create 的操作放行，即使 edit.path 不在 manifest 中；模型因此可能创建 .github/workflows、脚本或其他任意仓库文件。虽然 review 模式随后拒绝所有补丁，但其他模式仍会把这些操作交给 planOperations 执行。
- **Suggestion (analysis only, NOT applied):** 对所有操作统一要求路径属于显式允许列表；如确实需要创建文件，应单独限制到固定目录，并拒绝绝对路径、..、反斜杠和符号链接相关路径。

#### [HIGH] 输出目录可被符号链接劫持

- **Location:** `.github/zapier-review/worker.mjs`
- **Category:** arbitrary-file-write
- **Detail:** 程序直接在仓库工作目录创建并写入 .luna-output 下的多个固定文件，但未检查目录及目标文件是否为符号链接。被 checkout 的恶意符号链接可使 writeFileSync 或 appendFileSync 跟随链接，覆盖工作区外或其他任意可写路径。
- **Suggestion (analysis only, NOT applied):** 将输出写入工作区外的安全临时目录，或在创建和打开每个文件时使用 lstat、O_NOFOLLOW 及安全的目录句柄，拒绝符号链接并限制最终路径。

#### [HIGH] 解包文件名可逃逸输出目录并覆盖任意文件

- **Location:** `packtool.py:172`
- **Category:** path-traversal
- **Detail:** 记录名称允许 ../、多级目录和以斜杠开头的路径，随后直接通过 os.path.join 构造目标并写入。恶意 pack 可将文件写到 outdir 外部，或在绝对路径位置创建或覆盖文件。
- **Suggestion (analysis only, NOT applied):** 拒绝绝对路径和包含 . 或 .. 的路径组件；规范化后确认目标路径仍位于输出目录内，并在写入时避免跟随符号链接。

#### [HIGH] 以跳过权限确认模式运行自动化代理

- **Location:** `wakeup_claude.py:126`
- **Category:** unsafe-agent-execution
- **Detail:** 脚本使用 --dangerously-skip-permissions 恢复任务，工作区中的恶意文件或提示内容可能诱导 Claude 执行命令、读取敏感文件或修改任意内容。
- **Suggestion (analysis only, NOT applied):** 移除该选项，或在隔离沙箱中运行并限制工作目录、网络、文件写入和可执行命令；对不可信仓库只使用人工确认或只读模式。

#### [HIGH] 工作目录未经转义拼接进 PowerShell 命令

- **Location:** `wakeup_claude.py:143`
- **Category:** command-injection
- **Detail:** config.WORKSPACE 被直接放入单引号 PowerShell 字符串；路径中的单引号或其他特殊内容可改变命令结构并执行额外命令。
- **Suggestion (analysis only, NOT applied):** 不要拼接 -Command 字符串，直接以参数列表启动 Claude 并通过 cwd 传递目录；若必须使用 PowerShell，应使用安全的参数绑定并严格校验路径。

### MEDIUM (38)

#### [MEDIUM] 迁移写回会跟随符号链接

- **Location:** `_migrate_paths.py:192`
- **Category:** arbitrary-file-write
- **Detail:** 子目录中的所有 .py 名称都会被加入处理列表，根目录文件的 isfile 检查也会跟随符号链接；--apply 随后使用 open(path, 'w') 写回。攻击者若能在目标目录放置 .py 符号链接，可能使迁移覆盖工作区外的任意可写文件，并且检查与写入之间还存在竞态。
- **Suggestion (analysis only, NOT applied):** 拒绝符号链接并校验 realpath 必须位于工作区内；写入时使用不跟随符号链接的安全打开方式，并尽量通过目录句柄或原子替换避免竞态。

#### [MEDIUM] 探测失败仍可能以成功状态退出

- **Location:** `.github/workflows/gm_probe.py:21`
- **Category:** missing-error-handling
- **Detail:** call() 将网络异常转换为 (None, repr(e))，HTTP 错误和 JSON 解析错误也只由调用方打印响应；show_models() 和 show_chat() 没有返回失败状态或退出非零。因此认证失败、服务不可用或响应格式改变时，GitHub Actions 仍可能显示成功。
- **Suggestion (analysis only, NOT applied):** 让调用函数返回结构化成功状态，并在任一请求失败、状态码非 2xx 或响应格式不符合预期时累计失败并以非零状态退出。

#### [MEDIUM] 默认模型预算过大且输入没有上限校验

- **Location:** `.github/workflows/provider-review.yml:15`
- **Category:** resource-exhaustion
- **Detail:** max_tasks 的默认值是 100000000 个输入加输出 token，且类型为任意字符串，工作流层面没有最大值、数值格式或成本确认限制。只要执行器采纳该输入，一次手动运行就可能消耗巨额模型额度、长时间占用 Runner 或触发服务限额。
- **Suggestion (analysis only, NOT applied):** 在工作流或执行器入口将预算解析为有限整数并设置硬上限；对超出常规阈值的预算要求显式审批，并在每次模型调用前后执行累计用量和费用保护。

#### [MEDIUM] 恢复流程把 Base64 字符串当作原始文本切片

- **Location:** `.github/zapier-review/autonomous.mjs:129`
- **Category:** encoding
- **Detail:** 从 Git blob 恢复的内容被保存为 encoding:'base64'，但随后直接对 Base64 字符串使用 manifest 的 start_char/end_char。Base64 长度和字符位置不对应原始文本，恢复批次会提交错误内容或错误范围，导致审核结果和覆盖声明不可靠。
- **Suggestion (analysis only, NOT applied):** 先按声明的编码解码为原始文本，再依据同一套字符偏移切片；同时校验解码后的哈希、长度和边界，避免在字节偏移与字符偏移之间混用。

#### [MEDIUM] 未隔离不受信任源码便交给可产生修改的模型

- **Location:** `.github/zapier-review/autonomous.mjs:186`
- **Category:** prompt-injection
- **Detail:** 仓库源码通过 sourceUrl 作为模型附件读取，源码和 Issue 内容均可能包含伪装成指令的攻击者文本。模型结果中的 patches 随后会被收集并用于生成提交和草稿 PR；现有校验主要验证格式、传输标记和覆盖范围，不能证明模型没有被源码中的指令操纵。
- **Suggestion (analysis only, NOT applied):** 将源码严格标记为不受信任数据并使用结构化输入隔离指令；对模型生成的文件路径、操作类型和内容实施独立的 allowlist 校验，并在任何自动修改前要求人工审批。

#### [MEDIUM] 并发执行可能导致重复计费且丢失执行记录

- **Location:** `.github/zapier-review/billing-probe.mjs:38`
- **Category:** race-condition
- **Detail:** 多个进程可同时通过 `if(!state)` 检查，在任一进程写入 starting 状态前分别调用 `createActionRun`。这会产生重复的模型调用和潜在账单，其中一个 runId 还可能覆盖另一个，导致无法恢复或审计。
- **Suggestion (analysis only, NOT applied):** 在发起网络调用前使用原子创建锁文件或独占锁（如 `open` 的 `wx` 标志）；检测到已有锁或 starting 状态时应停止，不得继续提交新的调用。

#### [MEDIUM] 分箱成本遗漏每个块的额外开销

- **Location:** `.github/zapier-review/compare-packing.mjs:18`
- **Category:** logic-error
- **Detail:** 前置分块阶段将每个块的 `+4` 开销计入 `cost`，但 `flush` 生成的 item 以及后续 bin 合并只使用 `metadataCost` 和块 token 数，没有保留这些 `+4`。因此 `best.cost` 和最终报告会低估输入大小，可能把实际超过上下文预算的请求误判为合规。
- **Suggestion (analysis only, NOT applied):** 将块引用及其额外 framing 开销统一封装到一个成本计算函数，并在分箱和最终校验中使用同一计算结果；至少把每个块出现次数对应的 `+4` 纳入 item 成本。

#### [MEDIUM] starting 状态可能永久卡死且无法恢复

- **Location:** `.github/zapier-review/native-sdk.mjs:58`
- **Category:** error-handling
- **Detail:** 代码在创建远程任务前写入 starting；如果创建失败、进程在创建后保存前崩溃，或保存操作失败，记录可能停留在 starting。后续 executeNative 只接受 queued 或 running，因此既不能继续轮询，也不能安全判断是否已经创建了远程任务。
- **Suggestion (analysis only, NOT applied):** 将状态转换设计为可恢复的幂等状态机：为 starting 保存请求标识并支持查询恢复，或提供明确的人工确认流程；避免在远程创建与本地持久化之间留下不可处理的状态。

#### [MEDIUM] 完成结果未验证覆盖范围

- **Location:** `.github/zapier-review/native-sdk.mjs:75`
- **Category:** validation
- **Detail:** 这里只检查四个字段是字符串且能被 JSON.parse，未验证 covered_ranges_json 的 manifest 哈希、范围结构和完整覆盖；虽然 validateCompactCoverage 已定义，但 executeNative 未调用它。因此模型可能返回 status=completed 及空或错误范围，并被直接保存为成功结果。
- **Suggestion (analysis only, NOT applied):** 在保存 completed 结果前解析并严格校验所有结果结构，使用本次请求的精确 manifest 调用 validateCompactCoverage；校验失败时拒绝结果或标记为 incomplete。

#### [MEDIUM] NaN 目标值可绕过容量限制

- **Location:** `.github/zapier-review/prepare-capacity-probe.mjs:8`
- **Category:** input-validation
- **Detail:** Number('NaN') 会得到 NaN，而现有的上下界比较不会拒绝它。此后 target 相关算术和 actual>target 判断仍为 false，候选循环可能选入整个仓库并生成不受限制的超大 payload，导致内存或磁盘资源耗尽。
- **Suggestion (analysis only, NOT applied):** 使用 Number.isFinite 和 Number.isSafeInteger（或明确的有限数值校验）验证 target，并在选择文件和最终写入前都强制执行字节/token 上限。

#### [MEDIUM] 未脱敏的仓库源码被指向公开 URL

- **Location:** `.github/zapier-review/prepare-file-probe.mjs:21`
- **Category:** data-exposure
- **Detail:** source.txt 包含选定文件的完整原始内容，随后被组织为 raw.githubusercontent.com 下的固定公开路径。按该流程提交后，任何可能存在于所选源码中的凭据、内部信息或其他敏感数据都会成为无需认证即可访问的公共内容；代码没有保密性检查或秘密扫描。
- **Suggestion (analysis only, NOT applied):** 不要把未脱敏源码放入公开仓库路径；改用私有存储和短时签名 URL，或在提交前执行秘密扫描、敏感文件阻断和明确的公开授权检查。

#### [MEDIUM] 固定审计计划检查与写入之间存在竞态

- **Location:** `.github/zapier-review/prepare-full-audit.mjs:12`
- **Category:** race-condition
- **Detail:** 通过 existsSync 检查 schedule.json 后，程序才创建目录并在后续直接写入多个输出文件；并发运行时两个进程都可能通过检查，互相覆盖批次文件或生成混合快照，违反“不得覆盖固定计划”的约束。
- **Suggestion (analysis only, NOT applied):** 使用独占锁文件或 open(..., 'wx') 原子抢占任务，并在临时目录中完成写入后通过原子 rename 发布完整计划。

#### [MEDIUM] 请求超时未覆盖响应体读取且异常路径未清理定时器

- **Location:** `.github/zapier-review/provider-api.mjs:102`
- **Category:** resource-management
- **Detail:** fetch 返回响应头后立即清除定时器，但随后 resp.text() 或 resp.json() 可能无限期等待，因此 timeoutMs 不能限制完整请求。fetch 抛错或 HTTP 非成功时也会跳过 clearTimeout，重试多次后会遗留长时间定时器。OpenAI 和 Anthropic 两个调用函数均有此问题。
- **Suggestion (analysis only, NOT applied):** 让 AbortSignal 或定时器覆盖响应体读取的完整生命周期，并在 finally 中统一清理；对响应体解析也设置超时。

#### [MEDIUM] 模型输出的 JSON 结构校验不足

- **Location:** `.github/zapier-review/provider-api.mjs:199`
- **Category:** input-validation
- **Detail:** 这里只验证 *_json 字段是字符串且能被 JSON.parse，未验证 review_json 是否为预期数组、covered_ranges_json 是否包含正确的 manifest_sha256 和合法的闭区间，也未校验 finding 字段类型。畸形或不完整的模型响应可能被当作有效结果传给下游，造成崩溃或错误覆盖声明。
- **Suggestion (analysis only, NOT applied):** 使用显式 schema 校验所有输出字段、数组元素、哈希值和范围边界；校验失败时返回明确的 incomplete 或错误，不要接受仅可解析但结构错误的 JSON。

#### [MEDIUM] 外部请求成功后崩溃会使任务永久停滞

- **Location:** `.github/zapier-review/provider-autonomous.mjs:477`
- **Category:** reliability
- **Detail:** 调用 provider 前先将批次标记为 starting 并保存；如果 provider 已接受请求但进程在保存 providerResult 前崩溃或超时，恢复流程会直接因“Submission is uncertain”抛错，既不会查询原请求状态，也不会继续任务。
- **Suggestion (analysis only, NOT applied):** 为每个请求保存幂等键并使用 provider 的请求状态查询或重试机制；无法确认时应进入可恢复的人工核对状态，而不是永久停止整个作业。

#### [MEDIUM] 模型原始响应和证据可能被持久化为明文

- **Location:** `.github/zapier-review/provider-autonomous.mjs:521`
- **Category:** sensitive-data-exposure
- **Detail:** q.providerResult 包含模型返回的完整审核结果，随后由 save() 写入 cloud-job.json；后续报告还会把 finding 的 description 和 evidence 提交到结果分支。若被审核源码含有密钥、令牌或个人数据，模型的精确证据可能将其复制到提交、草稿 PR 和日志可访问的状态中。
- **Suggestion (analysis only, NOT applied):** 不要持久化完整原始响应；对证据中的密钥和敏感值进行脱敏或只保存哈希/截断值，并限制结果分支、artifact 和 PR 的访问权限。

#### [MEDIUM] 账本写入未统一受锁保护

- **Location:** `.github/zapier-review/validation-runner.mjs:14`
- **Category:** race-condition
- **Detail:** execute使用active.lock，但init和observe在未获取该锁的情况下直接调用save；多个observe也可并发写入同一个固定的ledger.json.tmp。并发时可能覆盖已保存的运行状态、反射或预算信息，或在rename阶段产生错误；进程被强制终止还可能留下永久阻塞后续操作的锁文件。
- **Suggestion (analysis only, NOT applied):** 让所有修改账本的命令使用同一原子锁或文件锁，并采用不会互相覆盖的临时文件；同时为遗留锁增加基于进程存活状态的安全恢复机制。

#### [MEDIUM] 下载附件时无大小上限，可能耗尽内存

- **Location:** `.github/zapier-review/verify-probe-attachments.mjs:14`
- **Category:** resource-exhaustion
- **Detail:** 脚本在校验声明的bytes之前先对整个HTTP响应调用arrayBuffer并复制为Buffer，因此服务端可以返回远超计划大小的内容，导致内存和CPU消耗失控。多个附件还可累积放大该问题。
- **Suggestion (analysis only, NOT applied):** 在读取响应时按单附件和总量设置硬上限，优先校验Content-Length，并通过流式读取逐块计算哈希；超过计划大小或允许上限时立即中止。

#### [MEDIUM] 去重检查与派发不是原子操作

- **Location:** `.github/zapier-review/worker.mjs`
- **Category:** race-condition
- **Detail:** find(dispatch:key)、预算统计、createTableRecords 和 webhook POST 之间没有原子占用或唯一约束。并发工作流可能同时观察到不存在标记并重复创建标记、重复派发和重复计费；反之，POST 失败或超时后已创建的 dispatched 标记又会永久阻止重试，后续请求最多等待 20 分钟后失败。
- **Suggestion (analysis only, NOT applied):** 使用带唯一约束的条件插入或事务式租约，只有成功取得租约的执行者才能派发；为明确失败和歧义超时增加可审计的恢复/过期机制，并对已有 queued 记录使用条件状态转换后再执行。

#### [MEDIUM] 传输日志缺少明确的敏感信息脱敏

- **Location:** `.github/zapier-review/worker.mjs`
- **Category:** sensitive-data-exposure
- **Detail:** journalFetch 产生的 row 被直接 JSON.stringify 后追加到 .luna-output/transport.jsonl，本文件没有过滤 Authorization、SDK 凭据、表记录正文或其他敏感字段。若该日志包含请求头或请求体，凭据和模型数据会随 Actions artifact 或工作区持久化。
- **Suggestion (analysis only, NOT applied):** 在日志边界强制移除或掩码所有认证头、client secret、回调认证值和敏感正文；限制日志文件权限，避免上传原始传输日志，并设置保留期限。

#### [MEDIUM] 外部结果在解析和持久化前没有大小上限

- **Location:** `.github/zapier-review/worker.mjs`
- **Category:** denial-of-service
- **Detail:** 来自 Zapier/模型的 review_json、patch_json、needs_context_json 和 covered_ranges_json 只检查字符串类型，随后直接 JSON.parse、追加到 answers/allEdits 并写入文件或提交 GitHub。异常或被篡改的上游结果可导致高内存、磁盘消耗或超大 GitHub 请求。
- **Suggestion (analysis only, NOT applied):** 在读取结果时先限制字段字节数、数组长度、元素数量和 patch 文本长度，并对总输出大小设置上限；超限时拒绝结果而不是继续解析或写盘。

#### [MEDIUM] 缺失连接时回退到固定 GitHub 连接

- **Location:** `.github/zapier-review/zap-dispatch.js:29`
- **Category:** authorization
- **Detail:** connections.github 未提供或为空时，代码会静默使用硬编码连接 ID 66435003，可能绕过调用方预期的账号和权限边界，并使用未验证的 GitHub 账户派发工作流。
- **Suggestion (analysis only, NOT applied):** 移除固定连接回退值；缺少连接时直接失败，并在派发前校验连接对应的 GitHub 身份和目标仓库。

#### [MEDIUM] 部署失败后没有回滚或保证重启

- **Location:** `deploy_server_update.py:45`
- **Category:** error-handling
- **Detail:** 脚本停止服务后才逐个复制文件并修改清单。任一源文件缺失、复制失败、清单解析失败或条目不存在都会直接异常退出，可能留下服务停止、二进制部分更新且清单未同步的状态。
- **Suggestion (analysis only, NOT applied):** 在停止前完成预检查并准备暂存目录；使用 try/finally 保证失败时恢复备份并尝试重启，完成全部校验后再切换文件。

#### [MEDIUM] 清单直接覆盖写入，可能留下损坏文件

- **Location:** `deploy_server_update.py:67`
- **Category:** unsafe-file-write
- **Detail:** manifest.json 通过 write_text 直接覆盖；进程中断、磁盘故障或并发读取时可能留下截断或半写的 JSON，导致后续启动或更新失败。
- **Suggestion (analysis only, NOT applied):** 先写入同目录临时文件并完成刷新与校验，再使用原子替换；保留并验证备份，必要时对更新过程加锁。

#### [MEDIUM] 诊断输出可能泄露环境凭据

- **Location:** `dumpenv.ps1:202`
- **Category:** sensitive-data-exposure
- **Detail:** 成功读取后会原样输出所有名称以 MU_ 或 OPENMU 开头的环境变量，其中可能包含本地用户名、密码或令牌；标准输出若被终端录制、CI 或日志系统收集，就会形成凭据泄露。
- **Suggestion (analysis only, NOT applied):** 默认只输出变量名或对密码、令牌等值进行掩码；将显示完整值设为明确的显式选项，并在脚本说明中警告不要将输出写入日志。

#### [MEDIUM] 未验证的环境变量被拼接进 PowerShell 参数

- **Location:** `make_shortcut.ps1:8`
- **Category:** command-injection
- **Detail:** MU_SERVER_ROOT 直接拼接到快捷方式的 Arguments 字符串中，没有进行 Windows 命令行转义或可信路径校验。若低信任调用方能控制该环境变量，嵌入引号即可改变 -File 参数并注入额外的 PowerShell 参数；同时使用 ExecutionPolicy Bypass 会放大影响。
- **Suggestion (analysis only, NOT applied):** 仅接受经过规范化且位于允许目录下的本地路径，拒绝引号、UNC 和其他异常输入；使用可靠的 Windows 参数转义方式，并避免不必要的 ExecutionPolicy Bypass。

#### [MEDIUM] 补丁清单使用非原子覆盖写入

- **Location:** `patch_manifest.py:49`
- **Category:** unsafe-file-write
- **Detail:** LIVE 清单直接以写入模式打开后执行 json.dump；中断或磁盘错误会先截断原文件，留下不可解析或不完整的 manifest.json。脚本也没有锁，可能与其他更新操作发生写入竞争。
- **Suggestion (analysis only, NOT applied):** 写入临时文件并校验 JSON 后使用原子替换，保留可恢复备份；对清单更新增加互斥锁或版本校验。

#### [MEDIUM] 管理员密码通过明文命令行参数传入

- **Location:** `provision_secrets.ps1:8`
- **Category:** credential-exposure
- **Detail:** $AdminPassword 是普通字符串参数，调用脚本时密码会出现在 PowerShell 历史、进程命令行或进程监控中。
- **Suggestion (analysis only, NOT applied):** 改用 SecureString/PSCredential，或通过标准输入、受保护文件或其他不出现在命令行中的方式传递密码。

#### [MEDIUM] 游戏密码被直接输出到标准输出

- **Location:** `provision_secrets.ps1:53`
- **Category:** secrets-in-logs
- **Detail:** GAME_PASS 会被写入终端、CI 日志或任务调度器日志，导致凭据泄露。
- **Suggestion (analysis only, NOT applied):** 不要输出密码；如确实需要交接，写入访问受限的凭据存储或提供显式的安全导出选项。

#### [MEDIUM] 登录凭据通过环境变量传给客户端

- **Location:** `start_client.ps1:23`
- **Category:** credential-exposure
- **Detail:** 游戏用户名和密码会出现在客户端进程环境块中，同用户进程、崩溃转储或子进程可能读取这些值。
- **Suggestion (analysis only, NOT applied):** 优先使用客户端支持的受保护凭据通道；若环境变量不可避免，应缩短凭据有效期并确保客户端不会转储或传播环境块。

#### [MEDIUM] 将父进程全部环境变量传给 Claude

- **Location:** `wakeup_claude.py:136`
- **Category:** secret-exposure
- **Detail:** os.environ.copy() 会把 API 密钥、代理凭据及其他无关秘密完整继承给 PowerShell/Claude；结合跳过权限模式，工作区内容可能诱导任务读取或泄露这些秘密。
- **Suggestion (analysis only, NOT applied):** 构造最小环境白名单，仅传递 Claude 必需的变量，并避免把凭据放入可被代理访问的环境中。

#### [MEDIUM] 模型目录以非原子方式覆盖

- **Location:** `zapier-proxy/refresh-models.mjs:40`
- **Category:** race-condition
- **Detail:** writeFileSync 直接覆盖 all_models.json；代理进程或其他读取者可能在写入过程中读到截断 JSON，导致 models.mjs 导入时 JSON.parse 失败并使服务启动或请求崩溃。
- **Suggestion (analysis only, NOT applied):** 先写入同目录临时文件并完成 JSON 校验，再使用原子 rename 替换目标文件，同时保留上一份有效目录作为回退。

#### [MEDIUM] API 密钥未配置时认证会失效

- **Location:** `zapier-proxy/server.mjs:32`
- **Category:** authentication
- **Detail:** PROXY_API_KEY 为空时所有请求都会被视为已认证；若部署时将 HOST 配置为 0.0.0.0 或其他公网地址，任何人都可以调用 Zapier 模型并消耗配额，通配符 CORS 还允许浏览器跨域调用。
- **Suggestion (analysis only, NOT applied):** 当监听非回环地址时强制要求 PROXY_API_KEY，未配置则拒绝启动；同时将 access-control-allow-origin 限制为明确的受信来源。

#### [MEDIUM] 上游调用缺少并发和生命周期限制

- **Location:** `zapier-proxy/server.mjs:93`
- **Category:** resource-exhaustion
- **Detail:** 每个请求都会创建一次 Zapier action run，并可能轮询最长五分钟；服务没有并发上限、速率限制，也没有在客户端断开时取消上游任务，公开部署时容易被用来耗尽 Zapier 配额和本地资源。
- **Suggestion (analysis only, NOT applied):** 增加认证后的速率限制、全局及每用户并发上限和队列容量；将请求断开信号传递给上游并取消轮询，设置明确的请求超时。

#### [MEDIUM] 上游异常详情直接返回给客户端

- **Location:** `zapier-proxy/server.mjs:101`
- **Category:** information-disclosure
- **Detail:** 响应消息直接拼接 e.message；上游 SDK 或 Zapier 返回的错误可能包含提供商、工作流、请求参数或其他内部信息。
- **Suggestion (analysis only, NOT applied):** 对客户端返回固定且简洁的 502 错误，仅在受控的内部日志或遥测中记录脱敏后的错误和关联 ID。

#### [MEDIUM] 服务本身仅使用明文 HTTP

- **Location:** `zapier-proxy/server.mjs:128`
- **Category:** transport-security
- **Detail:** 代码使用 node:http 创建服务，若 HOST 配置为非回环地址且外部没有可信的 TLS 终止层，Bearer 密钥和聊天内容会以明文传输。
- **Suggestion (analysis only, NOT applied):** 仅绑定回环地址并通过受信反向代理终止 HTTPS，或改用 node:https 并配置证书、私钥及安全 TLS 参数。

#### [MEDIUM] 提示词中的中文字符串存在 UTF-8 乱码

- **Location:** `zapier-proxy/zapier-client.mjs:34`
- **Category:** encoding-bug
- **Detail:** ZH_ROLE 以及 messagesToPrompt 中的中文提示词显示为“鐢ㄦ埛”“鍔╂墜”等乱码；这些字符串会直接发送给模型，可能导致角色标签和行为约束失效。
- **Suggestion (analysis only, NOT applied):** 从正确来源恢复中文字符串，确认文件、编辑器、构建流程和运行时均使用 UTF-8，并增加对生成提示词关键文本的编码测试。

#### [MEDIUM] 不同信任级别的消息被拼接为同一层纯文本

- **Location:** `zapier-proxy/zapier-client.mjs:56`
- **Category:** prompt-injection
- **Detail:** system、developer、历史消息和用户内容都被序列化到单个提示词中，用户内容可以伪造“系统要求”“之前的对话记录”或助手发言标签，从而诱导下游模型改变原本的系统约束。
- **Suggestion (analysis only, NOT applied):** 优先使用支持结构化角色和消息边界的上游接口；若只能使用单一提示词，应采用明确且不可混淆的边界标记，对不可信内容进行引用封装，并将不可覆盖的策略放在最终固定指令中。

### LOW (18)

#### [LOW] 插入引导代码会混用换行符

- **Location:** `_migrate_paths.py:161`
- **Category:** encoding-bug
- **Detail:** 文件以 newline='' 读取以保留 CRLF，但随后通过 split('\n') 和 join('\n') 插入 bootstrap 行。对 CRLF 文件执行迁移后，原有行保留 CRLF，而新增行使用 LF，导致混合换行并产生不必要的编码差异。
- **Suggestion (analysis only, NOT applied):** 检测原文件的换行符并用同一分隔符生成新增内容，或使用保留行尾的 splitlines(keepends=True) 进行插入。

#### [LOW] 批量写回失败时没有隔离或回滚

- **Location:** `_migrate_paths.py:199`
- **Category:** error-handling
- **Detail:** 批处理循环只捕获 SyntaxError；读取、解析以外的 OSError、UnicodeDecodeError 或写入失败会直接终止进程，而此前已成功写入的文件不会回滚，可能留下部分迁移状态。
- **Suggestion (analysis only, NOT applied):** 捕获并记录每个文件的 I/O 和编码错误，继续处理或明确失败退出；需要一致性时先写临时文件并在全部校验通过后原子替换，或提供回滚机制。

#### [LOW] 创建 Junction 失败不会可靠地使脚本失败

- **Location:** `_mklink_dbg_bom.ps1:3`
- **Category:** missing-error-handling
- **Detail:** New-Item 的错误默认是非终止错误，脚本随后仍执行 Test-Path；在创建失败时可能只输出错误或 False，但调用方未必收到失败退出码。
- **Suggestion (analysis only, NOT applied):** 设置 $ErrorActionPreference = 'Stop'，捕获 New-Item 异常并显式 exit 1；创建后同时验证 Junction 和目标路径。

#### [LOW] 创建 Junction 失败不会可靠地使脚本失败

- **Location:** `_mklink_dbg.ps1:3`
- **Category:** missing-error-handling
- **Detail:** New-Item 的错误默认是非终止错误，脚本随后仍执行 Test-Path；在创建失败时可能只输出错误或 False，但调用方未必收到失败退出码。
- **Suggestion (analysis only, NOT applied):** 设置 $ErrorActionPreference = 'Stop'，捕获 New-Item 异常并显式 exit 1；创建后同时验证 Junction 和目标路径。

#### [LOW] 源码路径未进行 URL 分段编码

- **Location:** `.github/zapier-review/autonomous.mjs:186`
- **Category:** encoding
- **Detail:** sourceUrl 直接拼接 q.source，路径中合法的 #、? 或百分号等字符会被解释为 URL 片段、查询参数或转义序列，导致模型下载错误文件或任务因哈希不匹配失败。raw() 也使用相同的未编码拼接方式。
- **Suggestion (analysis only, NOT applied):** 按路径段使用 encodeURIComponent 构造 URL，避免把文件名解释为查询或片段；下载后继续校验响应内容与预期 SHA-256 完全一致。

#### [LOW] 准备脚本与多文件探针的输入文件名不一致

- **Location:** `.github/zapier-review/prepare-full-audit.mjs:95`
- **Category:** automation
- **Detail:** 该脚本将输入写入 pending-inputs.json，而 prepare-multifile-probe.mjs 第 8 行固定读取同一目录下的 inputs.json。若直接将此脚本生成的批次作为探针的 from 参数，流程会因文件不存在而失败。
- **Suggestion (analysis only, NOT applied):** 统一两阶段使用的文件名，或在明确的转换步骤中原子地将 pending-inputs.json 发布为 inputs.json，并增加存在性检查。

#### [LOW] 响应体读取没有大小上限

- **Location:** `.github/zapier-review/provider-api.mjs:105`
- **Category:** resource-exhaustion
- **Detail:** 错误响应通过 resp.text() 完整读入后才截取前 500 个字符，成功响应的 resp.json() 也没有响应大小限制。异常或被错误配置的上游响应可能导致进程无界分配内存。
- **Suggestion (analysis only, NOT applied):** 先限制 Content-Length，并通过带上限的流式读取截断错误体和模型响应；超过上限时返回明确的响应过大错误。

#### [LOW] 所有错误都会重试，包括不可恢复的客户端错误

- **Location:** `.github/zapier-review/provider-api.mjs:115`
- **Category:** error-handling
- **Detail:** 重试逻辑没有区分网络错误、429/5xx 等瞬时故障与 400、401、403、404 等不可恢复错误。认证错误或无效请求会重复等待并再次调用接口，增加作业耗时和潜在费用。Anthropic 调用函数也存在相同逻辑。
- **Suggestion (analysis only, NOT applied):** 仅重试网络错误、超时、429 和 5xx，并遵循 Retry-After；对认证、参数和权限错误立即失败。

#### [LOW] 原始文件 URL 未编码路径

- **Location:** `.github/zapier-review/provider-autonomous.mjs:113`
- **Category:** encoding
- **Detail:** raw() 直接把 path 拼接到 URL 中。仓库中名称包含 #、?、空格或其他保留字符的文件会被 URL 解析为片段、查询参数或错误的路径，导致读取错误、哈希校验失败或审核中断。
- **Suggestion (analysis only, NOT applied):** 逐个路径段使用 encodeURIComponent 后再拼接，或改用 GitHub Contents API，并保留斜杠分隔结构。

#### [LOW] 小分段尺寸遇到代理对会导致死循环

- **Location:** `.github/zapier-review/result-store.mjs:11`
- **Category:** resource-exhaustion
- **Detail:** resultSegments在检测到高代理项时直接将end减一，但未保证end仍大于start。调用size为1且当前位置是代理对高项时，end会退回到start，循环永不前进并持续占用CPU。size为0或负数也可能造成同类问题。
- **Suggestion (analysis only, NOT applied):** 校验size为正的安全整数，并在代理项调整后确保end>start；无法形成有效分段时应抛出明确错误。

#### [LOW] GitHub 和 Git 子进程操作可能无限阻塞

- **Location:** `.github/zapier-review/worker.mjs`
- **Category:** missing-timeout
- **Detail:** gh 辅助函数调用 GitHub API 时没有 AbortSignal 或其他超时，git cat-file 子进程也没有独立的执行期限。现有四小时运行时限只在部分模型处理完成后检查，无法覆盖这些 await 阶段，因此网络或子进程卡住时工作流可能无限占用 runner。
- **Suggestion (analysis only, NOT applied):** 为所有外部请求设置总超时和取消信号，为 Git 子进程设置 watchdog 并在超时后 kill；配合有限次数的退避重试和明确的失败状态。

#### [LOW] 构建工具链路径硬编码且初始化错误被吞掉

- **Location:** `build_main.bat:2`
- **Category:** ci-automation
- **Detail:** Visual Studio 环境脚本和 CMake 都固定在 D: 盘路径；vcvars32.bat 的所有输出和错误被重定向丢弃，脚本可能在环境初始化失败或使用陈旧环境变量的情况下继续构建。
- **Suggestion (analysis only, NOT applied):** 通过 PATH、vswhere 或可配置变量定位工具链；检查 vcvars32.bat 的返回码，失败时立即退出并保留可诊断的错误日志。

#### [LOW] 配置脚本依赖硬编码的本机工具路径

- **Location:** `configure_main.bat:2`
- **Category:** ci-automation
- **Detail:** Visual Studio 环境脚本和 CMake 固定使用 D: 盘绝对路径，换机器、换盘符或使用不同安装版本时配置会直接失败；环境初始化错误也被静默丢弃。
- **Suggestion (analysis only, NOT applied):** 使用 PATH、vswhere 或可配置变量查找 Visual Studio 和 CMake，并检查环境脚本返回码后再执行 CMake。

#### [LOW] 工具链初始化和目录切换失败未被检查

- **Location:** `diag_build.bat:2`
- **Category:** automation-error-handling
- **Detail:** vcvars32.bat 的输出和错误被全部丢弃，call 失败后脚本仍继续执行；cd 失败也未检查，cmake --build . 可能在继承的当前目录中构建错误项目。
- **Suggestion (analysis only, NOT applied):** 对 call 和 cd 分别检查 ERRORLEVEL，失败立即退出；至少保留错误日志，不要静默吞掉工具链初始化失败原因。

#### [LOW] 默认唤醒时间与任务说明不一致

- **Location:** `wakeup_claude.py:192`
- **Category:** automation-bug
- **Detail:** 任务说明和 PROMPT 指定凌晨 2:00，但默认调用 get_target_wakeup_time 使用的是 4:00，自动任务会延迟两个小时执行。
- **Suggestion (analysis only, NOT applied):** 统一文档、提示词和默认参数，或将唤醒时间集中配置并在启动时校验。

#### [LOW] 模型名称类型未校验可能导致请求级崩溃

- **Location:** `zapier-proxy/models.mjs:65`
- **Category:** input-validation
- **Detail:** resolveModel 对客户端传入的 name 直接调用 trim；对象、数组等非字符串输入会抛出 TypeError，若上层未捕获可造成请求失败甚至进程级拒绝服务。
- **Suggestion (analysis only, NOT applied):** 先检查 typeof name === 'string'，对长度和格式进行限制；无效输入返回明确的 4xx 错误而不是抛出未处理异常。

#### [LOW] 轮询超时后未处理空结果

- **Location:** `zapier-proxy/probe.mjs:27`
- **Category:** error-handling
- **Detail:** 连续 120 次处于 waiting 状态时 finalData 仍为 undefined，随后向 writeFileSync 传入 undefined 并抛出异常，无法明确报告超时原因。
- **Suggestion (analysis only, NOT applied):** 轮询结束后显式检测超时，记录任务 ID 和状态并抛出明确错误；不要写入未定义的结果。

#### [LOW] 远端错误和结果被原样输出到日志

- **Location:** `zapier-proxy/probe.mjs:30`
- **Category:** secrets-in-logs
- **Detail:** API 返回的 errors 和 results 直接写入控制台，远端响应可能包含提示内容、内部标识或敏感数据，并被终端或 CI 日志长期保存。
- **Suggestion (analysis only, NOT applied):** 仅记录状态、错误类别和必要的截断标识，对结果做脱敏；调试详细输出应通过显式开关启用。

## Files reviewed

<details><summary>Show file list (60)</summary>

- `_migrate_paths.py` (8.5 KB, utf-8)
- `_mklink_dbg_bom.ps1` (0.2 KB, utf-8)
- `_mklink_dbg.ps1` (0.2 KB, utf-8)
- `.github/workflows/gm_probe.py` (1.9 KB, utf-8)
- `.github/workflows/gm-probe.yml` (0.3 KB, utf-8)
- `.github/workflows/luna-autonomous.yml` (4.6 KB, utf-8)
- `.github/workflows/luna-review.yml` (4.1 KB, utf-8)
- `.github/workflows/provider-review.yml` (4.6 KB, utf-8)
- `.github/zapier-review/assess-probe.mjs` (2.7 KB, utf-8)
- `.github/zapier-review/attachment-preflight.mjs` (0.6 KB, utf-8)
- `.github/zapier-review/autonomous-core.mjs` (4.7 KB, utf-8)
- `.github/zapier-review/autonomous-core.test.mjs` (3.1 KB, utf-8)
- `.github/zapier-review/autonomous.mjs` (22.0 KB, utf-8)
- `.github/zapier-review/billing-probe.mjs` (4.4 KB, utf-8)
- `.github/zapier-review/compare-packing.mjs` (3.6 KB, utf-8)
- `.github/zapier-review/compressed-transport.mjs` (1.2 KB, utf-8)
- `.github/zapier-review/coverage-repair.mjs` (1.0 KB, utf-8)
- `.github/zapier-review/coverage-repair.test.mjs` (0.8 KB, utf-8)
- `.github/zapier-review/file-operations.mjs` (4.8 KB, utf-8)
- `.github/zapier-review/file-operations.test.mjs` (3.4 KB, utf-8)
- `.github/zapier-review/lossless-blocks.mjs` (5.3 KB, utf-8)
- `.github/zapier-review/lossless-blocks.test.mjs` (0.9 KB, utf-8)
- `.github/zapier-review/measure-compression.mjs` (3.5 KB, utf-8)
- `.github/zapier-review/native-sdk.mjs` (6.8 KB, utf-8)
- `.github/zapier-review/prepare-capacity-probe.mjs` (4.3 KB, utf-8)
- `.github/zapier-review/prepare-file-probe.mjs` (2.5 KB, utf-8)
- `.github/zapier-review/prepare-full-audit.mjs` (10.2 KB, utf-8)
- `.github/zapier-review/prepare-multifile-probe.mjs` (3.3 KB, utf-8)
- `.github/zapier-review/provider-api.mjs` (11.9 KB, utf-8)
- `.github/zapier-review/provider-autonomous.mjs` (29.2 KB, utf-8)
- `.github/zapier-review/request-journal.mjs` (1.2 KB, utf-8)
- `.github/zapier-review/result-store.mjs` (2.8 KB, utf-8)
- `.github/zapier-review/usage-guard.mjs` (1.6 KB, utf-8)
- `.github/zapier-review/usage-guard.test.mjs` (0.6 KB, utf-8)
- `.github/zapier-review/validation-budget.mjs` (3.0 KB, utf-8)
- `.github/zapier-review/validation-runner.mjs` (5.5 KB, utf-8)
- `.github/zapier-review/verify-probe-attachments.mjs` (1.8 KB, utf-8)
- `.github/zapier-review/worker.mjs` (24.2 KB, utf-8)
- `.github/zapier-review/worker.test.mjs` (18.0 KB, utf-8)
- `.github/zapier-review/zap-dispatch.js` (2.5 KB, utf-8)
- `build_main.bat` (0.3 KB, utf-8)
- `config.py` (3.2 KB, utf-8)
- `configure_main.bat` (0.3 KB, utf-8)
- `deploy_server_update.py` (2.1 KB, utf-8)
- `diag_build.bat` (0.3 KB, utf-8)
- `dumpenv.ps1` (8.4 KB, utf-8)
- `local-credentials.ps1` (2.1 KB, utf-8)
- `make_shortcut.ps1` (0.7 KB, utf-8)
- `packtool.py` (8.6 KB, utf-8)
- `patch_manifest.py` (1.7 KB, utf-8)
- `provision_account.ps1` (2.7 KB, utf-8)
- `provision_secrets.ps1` (3.0 KB, utf-8)
- `start_client.ps1` (1.5 KB, utf-8)
- `wakeup_claude.py` (7.7 KB, utf-8)
- `zapier-proxy/check-client-enc.mjs` (0.5 KB, utf-8)
- `zapier-proxy/models.mjs` (3.8 KB, utf-8)
- `zapier-proxy/probe.mjs` (1.2 KB, utf-8)
- `zapier-proxy/refresh-models.mjs` (1.6 KB, utf-8)
- `zapier-proxy/server.mjs` (5.7 KB, utf-8)
- `zapier-proxy/zapier-client.mjs` (5.0 KB, utf-8)

</details>

---
Notes: findings come from a hosted model and require human verification before remediation. The audit inspected only user-authored glue/ops/automation code within the 50-task ceiling; large third-party engines were out of scope. The audit wrote no changes to any source file.