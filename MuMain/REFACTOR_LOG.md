# REFACTOR LOG — MuMain full cleanup (behavior-preserving)

> 每次拆分记录：原文件 → 新文件清单 + 搬走的函数名 + commit。
> 规则：不改变任何游戏行为/渲染结果/协议字节/数值公式；每步可编译 + ctest + 冒烟。

## 索引

- 2026-10-05 — 基线决策（用户确认）：上游合并 `1c063a61` 已把渲染器转向 SDL-GPU 且合并损坏（47 文件丢失、多处符号不匹配、无法编译）；与本方案（基于 GL Core Profile）冲突。故回到合并前**连贯的 GL 检查点 `31ca9237`**，放弃该 SDL-GPU 合并，并在其上重新应用 P0 工具（提交 `7d39768c`）。
  - 本地 vcpkg（`D:\vcpkg`，x86-windows：openssl + curl）与一键验证脚本就位。
  - **P0 验证通过**：`cmake --preset windows-x86` configure 成功；Debug 全量构建成功（Main + 全部测试）；`ctest` 245/245 通过、0 失败；tag `refactor-p0` 指向 `7d39768c`。

## 拆分记录

（按时间倒序追加）

### P1 — 机械清理（进行中）

- `a2518dcd` build(cmake): 对 MuClient 启用 `/W4`（非致命，不加 /WX）；抑制两个"设计如此"类别 `/wd4530`（旧客户端本就禁用异常展开，加 /EHsc 会改变行为）与 `/wd4201`（匿名 struct/union 有意使用）；新增 `docs/warning-triage.md` 警告分类与处置（含 27 条 C4701/C4703 疑似未初始化评审清单）。构建 + ctest 245/245 通过。
- `f39d2360` refactor(core): `SAFE_DELETE`/`SAFE_DELETE_ARRAY` 宏 → 类型安全 inline 模板 `SafeDelete`/`SafeDeleteArray`，置于新文件 `Core/Utilities/MemoryMacros.h`（_define.h 改为 include 该头）；54 文件、199 调用点机械替换，语义逐字等价；测试 stub 同步。构建 + ctest 245/245 通过。
  - 说明：裸所有权指针 → unique_ptr/shared_ptr 的深度转换无法在机械阶段安全证明所有权，按报告15"逐步替换"，随 P3–P7 逐文件进行；本步先消除宏（P1 结束新代码禁用）。
- `76122d48` refactor(core): 删除零调用的 `SAFE_RELEASE`、`BYTECAST` 宏。
- `7390daf0` refactor(core): 删除 `PtrReset` 宏，3 处调用改为直接 `reset()`（空指针 reset 安全）。
- `8d30e131` refactor(core): `Smart_Ptr`/`Weak_Ptr`/`SmartPointer` 宏 → `std::shared_ptr`/`std::weak_ptr` 与"前向声明 + `using XPtr`"，31 文件机械展开（token 等价）。
- DirectInput：经 grep 确认仍被 `Core/Utilities/Log/ErrorReport.cpp` 的 DirectX 版本探测逻辑使用（LoadLibrary DINPUT.DLL），并非无引用残留，故 P1 不删；`DIRECTINPUT_VERSION` 一并保留（按方案 caveat"先 grep 确认无引用"）。
- `25c5fa57` style(core): 41 个 CP949「仅注释」文件归一为 UTF-8（保留 CRLF），消除全部 C4828（271,566）。
- `bf3aeafb` style(core): 对齐 OBJECT/CHARACTER 前向声明到 class 定义，C4099 813→13。
- `20dc1883` style(render): 删除 MoveHandlers.cpp 中 59 个未引用平凡局部变量（C4101）。
- **P1 警告阶段小结**：全量干净重建权威警告数 **1,244**（原始 274,134，降 99.5%），构建 + ctest 245/245 全绿。剩余为变量隐藏(851)/有符号无符号(227)/C4189 副作用(112)/疑似未初始化(27)，按 R1 不批量处理，随 P3–P7 逐文件进行；明细、C4189 陷阱与 C4701/4703 清单见 `docs/warning-triage.md`。

---

## Bugs found during refactoring

> 顺手发现的既有 bug 不在重构提交里修；记在此处，单独 commit（或用户确认后）处理。

（暂无）

## P2 · Core/Globals 拆解（进行中）

- `596c9558` test(arch): 新增依赖方向守卫 `tests/arch/check_includes.py`（基线锁定型），CTest 用例 `arch_core_includes`；ctest 总数 245→246。当前 Core→高层反向包含基线 21 条，守卫仅对基线之外的新增失败。
- `42d81ded` refactor(scenes): 从 `_define.h` 抽出场景域常量（EGameScene、MAX_SERVER_PER_GROUP、组合按钮数）到 `Scenes/SceneConstants.h`，`_define.h` 改为 include（垫片）。
- 紧随其后：把该过渡垫片 include 纳入守卫基线（21→22）；该垫片随 `_define.h` 在 P9 删除而消失。构建 + ctest 246/246 全绿。
- 说明：分域抽出到更高层时，旧聚合头 `_define.h` 的兼容垫片会临时产生一条 Core→高层 include，属预期过渡项并逐条纳入基线；P9 删除聚合头时全部 resolve。
- `(本次)` refactor(engine): 从 `_define.h` 抽出寻路域（TW_* 地形行走标志、FACTOR_PATH_DIST、MAX_COUNT_PATH、EPathNodeState/EPathDirection）到 `Engine/Pathing/PathConstants.h`（含 WinCompat 提供 BYTE/位运算符）；守卫基线 22→23（过渡垫片）。构建 + ctest 246/246 全绿。

### P2 聚合头拆分（完成）
- `a3e478f0` refactor(core): `_define.h` 分域完成——抽出 31 个域头（GameLogic/Items·Skills·Guild·Pets、Character、UI/Text·Shop、Camera、App 等），聚合头变为按原序 include 的薄垫片；守卫基线 34→65（均为过渡垫片，P9 随聚合头删除而 resolve）。
- `3df89f87` refactor(core): 5,142 行 `_enum.h` 拆为 11 个 `Core/Enums/` 头（SEASON3A/3B/info/COMGEM namespace 原样保留重开，全局枚举按 ~600 行切块、边界对齐 enum 不切断），聚合头变薄垫片；Core 内部拆分不新增反向依赖，守卫基线保持 65。
- 全程全量构建通过、ctest 246/246 全绿。
- 待办：371 个 extern(g_pXxx) 收敛；消费者随 P3–P7 改为直接 include 后，于 P9 删除垫片与聚合头、守卫转正。