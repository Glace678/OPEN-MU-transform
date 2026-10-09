# P1 /W4 警告分类与处置（triage）

数据来源：在 `MuClient` 目标上启用 `/W4`（**不**加 `/WX`，警告不致命）后的 Debug 全量构建（`out/w4-build.log`，原始清单 `out/w4-warnings.txt`）。

原则（红线 R1）：**不借修警告改变运行时行为**。能机械对齐且无语义风险的直接修；属"设计如此"的抑制并说明；可能改变类型提升/初始化/控制流的，以及疑似潜在缺陷的，不在 P1 批量处理，留待逐文件阶段或单独决策。

## 总量

- 原始告警计数：274,134
- 其中 **C4828（源码字符集）271,566**：CP949/EUC-KR 头被按 UTF-8 读取所致的编码噪声，由 P1「源码编码归一（CP949→UTF-8）」清除，**不抑制**。
- 剔除 C4828 后的真实告警：约 **2,568**。

## 分类与处置

| 警告码 | 数量 | 含义 | 处置 |
|---|---|---|---|
| C4099 | 813 | 类型首次以 `class`/`struct` 不一致方式声明 | 安全：以后置定义为准机械对齐前向声明（分批，纯头/包含处） |
| C4459 | 459 | 局部变量隐藏全局变量 | 推迟：逐文件重命名，批量改易引入错误 |
| C4456 | 417 | 局部变量隐藏更早的局部 | 推迟：同上 |
| C4530 | 389 | 使用了异常处理但未启用展开语义（缺 /EHsc） | **设计如此，已 `/wd4530`**：旧客户端本就禁用异常展开；加 /EHsc 会改变行为 |
| C4245 | 178 | 有符号/无符号不匹配（初始化） | 推迟：改动可能改变整型提升，需逐处确认 |
| C4189 | 112 | 局部变量已初始化但未使用 | 安全批：删除前确认初始化表达式无副作用（有副作用则保留调用） |
| C4101 | 59 | 未引用的局部变量 | 安全批：直接删除 |
| C4018 | 44 | 有符号/无符号不匹配（比较） | 推迟：同 C4245 |
| C4457 | 40 | 局部变量隐藏函数参数 | 推迟：逐文件处理 |
| C4701 | 25 | 局部变量可能未初始化即使用 | **疑似潜在缺陷**：不静默改，列出后单独评估（修复会改变初始化行为） |
| C4201 | 8 | 非标准扩展：匿名 struct/union | **设计如此，已 `/wd4201`**：数学/互操作类型有意使用 |
| C4996 | 7 | 使用了弃用 API | 逐处评估 |
| C4244 | 4 | 转换可能丢失数据 | 推迟：逐处确认 |
| C4458 | 4 | 局部隐藏类成员 | 推迟：逐文件处理 |
| C4477/C4703/C4706/C4554/C4389/C4100/C5205 | 各 1–2 | 格式串/未初始化/赋值作条件等 | 逐处评估；C4703 同 C4701 列为疑似缺陷 |

## 已落地

**进展（2026-10-05，全量干净重建口径）**：安全批次后，全项目警告从原始 274,134 降至 **1,244**（降幅 99.5%），构建 + ctest 245/245 全绿。已清除：C4828（271,566，编码归一）、C4101（59）、C4099（800/813）、C4530/C4201（抑制）。剩余 1,244 的分布：变量隐藏 C4459/4456/4457/4458 共 851；有符号/无符号与转换 C4245/4018/4244/4389 共 227；C4189 共 112（含 rand()/函数副作用，见下）；C4701/4703 共 27（疑似缺陷，见附录）；其余为 C4099 13、C4996 7 及零散项。这些均**不在 P1 批量处理**，随 P3–P7 逐文件重构处理。

> **C4189 陷阱（务必逐处确认）**：多处"已初始化未引用"变量的初始化表达式含 `rand()`（如 `int iAngle = rand() % 360;`）、带副作用的函数调用（如 `g_GuildCache.SetGuildMark(...)`、`CheckTarget(c)`）或引用懒初始化（如 `GetItemAddOtioninfo(...)`）。直接删除会减少 rand() 调用次数/改变懒初始化（违反 R1）。处理方式：要么保留，要么改为 `(void)<expr>;` 以保留调用；RAII 类型（如 lock_guard）即使未引用也绝不能删。

- `/W4` 对 `MuClient` 生效（非致命）。
- `/wd4530`、`/wd4201` 两个"设计如此"类别已抑制并注释。
- C4828 不抑制，随编码归一任务清零。

## 待办（P1 内）

1. 分批做 C4099 对齐（每批 ≤600 行，构建 + ctest + commit）。
2. C4101 / C4189 安全批（逐处确认无副作用）。
3. 源码 CP949→UTF-8 与 LF 行尾归一（消除 C4828；韩文只改编码不翻译，翻译留 P9）。
4. include 整理（仅本轮触碰文件，clang-tidy `misc-include-cleaner` 辅助）。
5. C4701/C4703 疑似未初始化缺陷清单单独评审。

## 附录：C4701/C4703 疑似未初始化变量评审清单（共 27 条，去重）

> 这些位置编译器无法证明所有路径都已初始化。多数可能是误报（条件守卫编译器不识别），但少数可能是真实缺陷。**禁止在行为保持重构中通过直接初始化为 0 来'消除'**：若是误报则仅多一次存储；若真有路径使用未初始化值，初始化为 0 会改变行为。需逐个阅读控制流后单独决策。

- `src/source/Engine\AI\ZzzAI.cpp(599) : warning C4701: potentially uninitialized local variable 'cx' used
- `src/source/Engine\AI\ZzzAI.cpp(600) : warning C4701: potentially uninitialized local variable 'cy' used
- `src/source/Engine\Object\ZzzInventory.cpp(2725) : warning C4701: potentially uninitialized local variable 'iWeaponSpeed' used
- `src/source/Engine\Object\ZzzInventory.cpp(2726) : warning C4701: potentially uninitialized local variable 'iNeedStrength' used
- `src/source/Engine\Object\ZzzInventory.cpp(2727) : warning C4701: potentially uninitialized local variable 'iNeedDex' used
- `src/source/Engine\Object\ZzzInventory.cpp(5614) : warning C4701: potentially uninitialized local variable 'ExpireTime' used
- `src/source/Engine\Object\ZzzInventory.cpp(5614) : warning C4703: potentially uninitialized local pointer variable 'ExpireTime' used
- `src/source/Network\Server\WSclient.cpp(10755) : warning C4701: potentially uninitialized local variable 'index' used
- `src/source/Network\Server\WSclient.cpp(10806) : warning C4701: potentially uninitialized local variable 'index' used
- `src/source/Network\Server\WSclient.cpp(10857) : warning C4701: potentially uninitialized local variable 'index' used
- `src/source/Render\Effects\Behaviors\MoveHandlers.cpp(8224) : warning C4701: potentially uninitialized local variable 'iBlurIdentity' used
- `src/source/Render\Effects\Behaviors\MoveHandlers.cpp(8224) : warning C4701: potentially uninitialized local variable 'iBone01' used
- `src/source/Render\Effects\Behaviors\MoveHandlers.cpp(8224) : warning C4701: potentially uninitialized local variable 'iBone02' used
- `src/source/Render\Effects\Behaviors\MoveHandlers.cpp(8224) : warning C4701: potentially uninitialized local variable 'iTypeBlur' used
- `src/source/Render\Effects\ZzzEffect.cpp(10021) : warning C4701: potentially uninitialized local variable 'Scale' used
- `src/source/Render\Effects\ZzzEffect.cpp(7873) : warning C4701: potentially uninitialized local variable 'Height' used
- `src/source/Render\Effects\ZzzEffectJoint.cpp(3405) : warning C4701: potentially uninitialized local variable 'Distance' used
- `src/source/Render\Effects\ZzzEffectParticle.cpp(7458) : warning C4701: potentially uninitialized local variable 'fScale' used
- `src/source/Render\Effects\ZzzEffectParticle.cpp(7461) : warning C4701: potentially uninitialized local variable 'fLight' used
- `src/source/UI\Legacy\UIMng.cpp(574) : warning C4701: potentially uninitialized local variable 'j' used
- `src/source/UI\Legacy\UIMng.cpp(577) : warning C4701: potentially uninitialized local variable 'i' used
- `src/source/UI\Legacy\UIMng.cpp(588) : warning C4701: potentially uninitialized local variable 'pWin' used
- `src/source/UI\Legacy\UIMng.cpp(588) : warning C4703: potentially uninitialized local pointer variable 'pWin' used
- `src/source/UI\Legacy\UIMng.cpp(647) : warning C4701: potentially uninitialized local variable 'nXCoord' used
- `src/source/UI\Legacy\UIMng.cpp(647) : warning C4701: potentially uninitialized local variable 'nYCoord' used
- `src/source/UI\NewUI\Character\NewUICharacterInfoWindow.cpp(450) : warning C4701: potentially uninitialized local variable 'iAddPoint' used
- `src/source/UI\NewUI\Character\NewUICharacterInfoWindow.cpp(450) : warning C4701: potentially uninitialized local variable 'iMinusPoint' used`n
