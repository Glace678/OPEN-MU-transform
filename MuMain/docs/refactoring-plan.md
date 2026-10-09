# MuMain 全量代码改进 · 执行看板

> 精简执行版。完整逐函数明细见《全量代码改进方案_总案.md》（合并定稿版 2026-10-05，20 份源报告整合；该总案文档当前不在本仓库内，需要时向负责人索取）。
> 本文件随每个文件/阶段完成勾选。行为保持型重构（behavior-preserving）：不改游戏行为、渲染结果、协议字节、数值公式、enum 数值。

## 铁律（红线）

1. **R1 行为保持**：含 `rand()` 调用次数/顺序、浮点运算顺序、static 读写顺序、懒初始化守卫一律不动。
2. **R2 不 push**：只本地 commit；禁止 PR / 上传任何远端。
3. **R3 每步可编译 + ctest + 冒烟**：抽块 → namespace → 转调 → 编译 → ctest → 冒烟 → commit；编译不过先修。
4. **R4 热路径**：渲染每帧路径/网络包处理不新增堆分配、字符串临时对象、虚调用链。
5. **R5**：不重命名既有类/全局；不引第三方库；不动 `src/ThirdParty/`。
6. 单 commit diff ≤ ~600 行（纯搬运除外）；message 格式 `refactor(<module>): <what> (behavior-preserving)`。

## 阶段进度

- [x] 分支 `refactor/full-cleanup` 已建立。**基线经用户确认改为连贯 GL 检查点 `31ca9237`**（放弃损坏的上游 SDL-GPU 合并 `1c063a61`）。
- [x] **P0 基线固化**：看板/脚本/日志就位；configure + Debug 全量构建通过；ctest 245/245；tag `refactor-p0` → `7d39768c`。
- [~] **P1 机械清理（进行中）**：
  - [x] 死宏/别名宏清理（Smart 系列、PtrReset、SAFE_DELETE*、SAFE_RELEASE、BYTECAST）。
  - [x] 源码编码归一（41 个 CP949 仅注释文件 → UTF-8），C4828 清零。
  - [x] `/W4` 启用（非致命）+ 设计如此类别抑制 + 安全批；权威警告 274,134 → **1,244**。
  - [ ] include 整理（仅触碰文件）；LF 行尾全仓归一（建议加 `.gitattributes` 单独提交）。
  - [ ] 剩余 1,244 警告（变量隐藏/有符号无符号/C4189 副作用/疑似缺陷）随 P3–P7 逐文件处理；C4701/4703 单独评审。
- [~] **P2 Core/Globals 拆解（进行中）**：
  - [x] include 方向守卫 `tests/arch/check_includes.py`（CTest `arch_core_includes`，基线锁定，当前基线 23）。
  - [x] `_define.h` 分域：Scene 常量 → `Scenes/SceneConstants.h`；寻路常量 → `Engine/Pathing/PathConstants.h`。
  - [ ] 其余域（Camera/Models/BodyPart/Kinds/Guild/Inventory/Storage 等）继续抽出；消费者改为直接 include 后移除垫片、缩小基线。
  - [ ] `_enum.h`(5,433) 分域；371 extern 收敛（统计脚本 `tools/list_globals.py` 待补；当前可临时用 `grep -Rc '^extern' src/source` 粗估）。
- [ ] **P3 Network 拆分**（WSclient 按协议域切割）
- [ ] **P4 Engine 拆分**（ZzzCharacter/ZzzObject/ZzzInventory）
- [ ] **P5 Render/Effects 拆分**（三巨头 + MoveHandlers）
- [ ] **P6 UI 拆分**（NewUI 巨型对话框 + Legacy 隔离）
- [ ] **P7 World 去重 + GameLogic 下沉**
- [ ] **P8 C# ClientLibrary + ConstantsReplacer**
- [ ] **P9 收尾**（守卫转正、韩语注释、删垫片、文档定稿、最终验收）

## 巨型文件看板（基线 2026-10-05，31 个文件 ≥ 2,000 行）

重新生成：`python tools/list_big_files.py [--threshold N] [--json]`。基线快照：`out/big-files-baseline.txt`。

### Network（P3）
- [ ] `src/source/Network/Server/WSclient.cpp` — 15,893
- [ ] `src/source/Network/Server/WSclient.h` — 3,717

### Engine（P4）
- [ ] `src/source/Engine/Object/ZzzCharacter.cpp` — 15,469
- [ ] `src/source/Engine/Object/ZzzInventory.cpp` — 7,987
- [ ] `src/source/Engine/Object/ZzzObject.cpp` — 7,580
- [ ] `src/source/Engine/Object/ZzzOpenData.cpp` — 4,473
- [ ] `src/source/Engine/Object/ZzzInterface.cpp` — 4,273
- [ ] `src/source/Engine/Object/ZzzInfomation.cpp` — 3,650

### Render（P5）
- [ ] `src/source/Render/Effects/Behaviors/MoveHandlers.cpp` — 10,537
- [ ] `src/source/Render/Effects/ZzzEffect.cpp` — 9,799
- [ ] `src/source/Render/Effects/ZzzEffectParticle.cpp` — 9,378
- [ ] `src/source/Render/Effects/ZzzEffectJoint.cpp` — 7,372
- [ ] `src/source/Render/Renderer/MuRendererSDLGpu.cpp` — 4,767
- [ ] `src/source/Render/Models/ZzzBMD.cpp` — 3,550
- [ ] `src/source/Render/Terrain/ZzzLodTerrain.cpp` — 3,287

### UI（P6）
- [ ] `src/source/UI/NewUI/Dialogs/NewUICustomMessageBox.cpp` — 7,409
- [ ] `src/source/UI/Legacy/UIControls.cpp` — 6,422
- [ ] `src/source/UI/Legacy/UIWindows.cpp` — 5,820
- [ ] `src/source/UI/NewUI/Dialogs/NewUICommonMessageBox.cpp` — 3,638
- [ ] `src/source/UI/NewUI/NewUIMuHelper.cpp` — 3,371
- [ ] `src/source/UI/NewUI/HUD/NewUIMainFrameWindow.cpp` — 2,791
- [ ] `src/source/UI/NewUI/NewUISystem.cpp` — 2,533

### World（P7）
- [ ] `src/source/World/GameMaps/GMEmpireGuardian1.cpp` — 2,996
- [ ] `src/source/World/GameMaps/GM_Raklion.cpp` — 2,946
- [ ] `src/source/World/GameMaps/GMCrywolf1st.cpp` — 2,743
- [ ] `src/source/World/GameMaps/GMHellas.cpp` — 2,275
- [ ] `src/source/World/GameMaps/GMBattleCastle.cpp` — 2,105

### Core / App / Dotnet（P2 / P8）
- [ ] `src/source/Core/Globals/_enum.h` — 5,433（P2 地基，最先拆）
- [ ] `src/source/App/Platform/Windows/Winmain.cpp` — 2,317
- [ ] `src/source/Dotnet/PacketFunctions_ClientToServer.cpp` — 2,131
- [ ] `src/source/Dotnet/PacketFunctions_ClientToServer.h` — 2,083

## 目标终态目录

```
src/source/
├── App/ Audio/ Camera/ Dotnet/        # 已存在
├── Core/       # 数学/时间/内存/配置/日志/Utilities/Globals 拆散
├── Data/       # 纯数据表加载校验
├── GameLogic/  # 战斗/物品/技能规则（可单测）
├── Network/    # Handlers/Buffs/Protocol/Dispatcher
├── Render/     # Models/Terrain/Effects/Textures/Shaders/IR
├── UI/         # 一窗一文件；Legacy 归档
├── World/      # GM_*.cpp 每图一文件 + 共享基类
└── Character/  # 角色元数据与呈现
```

依赖方向：`App → UI/Render → GameLogic → Core`；`Network → GameLogic/Core`；`World/Character` 依赖 GameLogic+Render，绝不反向。

## 本地工具链

- VS（MSVC 14.50，x86）+ Ninja Multi-Config，构建树 `out/build/windows-x86`。
- vcpkg 本地经典模式：`D:\vcpkg`（x86-windows：openssl、curl）。安装脚本 `scripts/setup_vcpkg_local.sh`。
- 一键验证：`scripts/refactor-smoke.sh [--configure]`（configure → build Main+tests → ctest）。

## 冒烟 5 步（每 Phase 结束，连本地 OpenMU 服）

1. 登录 2. 进图 3. 走动 4. 打怪 5. 开背包。另：每拆完一个网络协议域，做该域操作冒烟。
