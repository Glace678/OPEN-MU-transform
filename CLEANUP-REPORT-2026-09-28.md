# 项目清理报告 — 2026-09-28

工作区：`D:\openmu自用`
本次范围：**只删除无关截图**（其余任务按用户要求暂停）

---

## 一、已完成：删除无关截图

- **删除方式**：永久删除（沙箱禁止调用回收站接口，Add-Type 与 COM 均被拦截，已二次确认）
- **删除数量**：191 个 PNG
- **释放空间**：约 256 MB

| 位置 | 删除数量 | 说明 |
| --- | --- | --- |
| 项目根目录 `*.png` | 159 | 全部为调试/验证截图（`openmu-*`、`_verify_*`、`_shotA/B`、`_crop_*`、`_probe_screen`、`android-native-*`、`game-layout-after`） |
| `_vcheck/*.png` | 22 | 界面像素测量截图（`c_*`、`opt*`、`step*`、`sysmenu` 等） |
| `ui-repair/*.png` | 10 | 界面修复过程截图（`selection-*`、`inventory`、`menu`、`options` 等） |

### 保留项

| 文件 | 原因 |
| --- | --- |
| `position-texture.png` (2.8 KB) | 不是截图，疑似被资源管线引用的贴图；全项目检索未发现代码引用，请确认后可删 |

### 完整删除清单

<details>
<summary>点击展开 191 项</summary>

**根目录**
```
_crop_bottom.png                _crop_elf.png                   _crop_minimap.png
_probe_screen.png               _shotA.png                      _shotB.png
_verify_opt1.png                _verify_opt3.png                _verify_state1.png
_verify_state2.png              _verify_state3.png              _verify_state4.png
_verify_sysmenu.png             _verify_sysmenu2.png
android-native-game.png         android-native-start.png        game-layout-after.png
openmu-bootstrap.png            openmu-bootstrap-2.png          openmu-bootstrap-diagnostic.png
openmu-current.png              openmu-dark-mobile-tutorial.png openmu-debug-auth.png
openmu-debug-credentials.png    openmu-debug-credentials2.png   openmu-debug-drag.png
openmu-debug-login.png          openmu-debug-password.png       openmu-debug-ready.png
openmu-demo-aftergroup.png      openmu-demo-character-select.png openmu-demo-connection.png
openmu-demo-credentials.png     openmu-demo-cs-trace.png        openmu-demo-cursorfix.png
openmu-demo-enter-key.png       openmu-demo-entry.png           openmu-demo-focus-login.png
openmu-demo-focus-password.png  openmu-demo-hold.png            openmu-demo-isolated-login.png
openmu-demo-login-fast.png      openmu-demo-login-form.png      openmu-demo-login-input.png
openmu-demo-login-result.png    openmu-demo-quickselect.png     openmu-demo-serverlist.png
openmu-demo-serverlist2.png     openmu-demo-touchfix.png        openmu-diag-login.png
openmu-diagnostic-entry.png     openmu-emma-approach.png        openmu-emma-approach2.png
openmu-emma-shop-near.png       openmu-emma-shop-open.png       openmu-emma-target3.png
openmu-emma-target4.png         openmu-emma-target5.png         openmu-emma-touch-shop.png
openmu-enter-world.png          openmu-fast-login-result.png    openmu-final-bootstrap.png
openmu-final-bootstrap-progress.png  openmu-final-bootstrap-settings.png
openmu-final-bootstrap-settings-network.png  openmu-final-character-selection.png
openmu-final-credentials.png    openmu-final-credentials-ready.png  openmu-final-cs.png
openmu-final-inventory-touch.png openmu-final-launch.png        openmu-final-login-result.png
openmu-final-modal-before.png   openmu-final-modal-one-tap.png  openmu-final-native-login.png
openmu-final-play-second.png    openmu-final-popup-firsttouch.png  openmu-final-port-config.png
openmu-final-port-saved.png     openmu-final-server-list.png    openmu-final-settings-server.png
openmu-final-world.png          openmu-first-world.png          openmu-host-dns-character-select.png
openmu-host-native-start.png    openmu-hotfix-credentials.png   openmu-hotfix-entry.png
openmu-hotfix-native.png        openmu-ime-fix-world.png        openmu-ime-geometry.png
openmu-inventory-after-shop-close.png  openmu-inventory-current.png
openmu-inventory-native.png     openmu-inventory-touch-second-press.png
openmu-keyboard-build-start.png openmu-keyboard-pan-password.png openmu-login-after-keyboard.png
openmu-login-mode.png           openmu-login-mode-network.png   openmu-login-native.png
openmu-move-left.png            openmu-move-right.png           openmu-native-boot.png
openmu-new-login-form.png       openmu-new-login-result.png     openmu-new-native-login.png
openmu-new-server-row.png       openmu-new-server-select.png    openmu-noria-current.png
openmu-npc-shop.png             openmu-pan-build-network.png    openmu-pan-build-settings.png
openmu-password-ime-fixed.png   openmu-resource-ready.png       openmu-resume-after-start.png
openmu-resume-edge-authenticated.png  openmu-resume-edge-bootstrap.png
openmu-resume-edge-characters.png  openmu-resume-edge-credentials.png
openmu-resume-edge-inventory.png   openmu-resume-edge-item-inspect.png
openmu-resume-edge-item-moved.png  openmu-resume-edge-login.png   openmu-resume-edge-native.png
openmu-resume-edge-world.png    openmu-resume-inventory.png     openmu-resume-item-drag.png
openmu-resume-item-firsttouch.png  openmu-resume-start.png
openmu-resume-world-input-aftercredits.png  openmu-resume-world-input-auth.png
openmu-resume-world-input-char.png  openmu-resume-world-input-chars.png
openmu-resume-world-input-chars-ready.png  openmu-resume-world-input-current.png
openmu-resume-world-input-current2.png  openmu-resume-world-input-drag.png
openmu-resume-world-input-entry.png  openmu-resume-world-input-inventory.png
openmu-resume-world-input-login.png  openmu-resume-world-input-loginresult.png
openmu-resume-world-input-world.png  openmu-settings.png        openmu-settings-2.png
openmu-settings-after-help.png  openmu-settings-dark.png        openmu-settings-dark-2.png
openmu-settings-dark-3.png      openmu-settings-dropdown.png    openmu-settings-dropdown-2.png
openmu-settings-dropdown-fixed.png  openmu-settings-fixed.png   openmu-settings-help.png
openmu-settings-high.png        openmu-settings-persisted.png   openmu-settings-saved.png
openmu-settings-slider.png      openmu-touch-inventory.png      openmu-tutorial.png
```

**`_vcheck/`**
```
after_esc.png  after_scanz.png  after_z.png  c_bottom.png  c_bottomfull.png  c_center.png
c_center2.png  c_exact.png  c_lbl1.png  c_pillar.png  c_right.png  c_right2.png  now.png
opt1.png  opt2.png  opt3.png  phys.png  state4.png  step0_world.png  step1_sysmenu.png
sysmenu.png  zoom_panel.png
```

**`ui-repair/`**
```
attributes.png  character-frame-fixed.png  final-selection.png  inventory.png  menu.png
options.png  selection-1920.png  selection-print.png  selection-restored.png  world-check.png
```

</details>

---

## 二、暂停中：后续待办

### 2.1 继续删除（保守档剩余部分，约 84 MB）

| 目标 | 文件数 | 大小 |
| --- | --- | --- |
| 根目录 `*.log`（`_apk_*.log`、`android-emulator-*.log`） | 9 | 0.1 MB |
| 根目录 `build_log.txt`、`_probe_out.txt`、`_probe2.txt` | 3 | — |
| 根目录临时脚本 `_migrate_paths.py`、`_mklink_dbg*.ps1` | 3 | — |
| `_premigration_backup.zip` | 1 | 0.2 MB |
| `_vcheck/` 剩余（16 个 ps1 + 2 个 log） | 18 | — |
| `ui-repair/` 剩余（16 项） | 16 | 20 MB |
| `__pycache__/` | 1 | — |

### 2.2 中文文件名 → 英文（目录 4 项 + 文件 17 项）

> ⚠️ **不建议重命名**：`object01_盔夯.bmd`、`world34狼 官肺 啊扁.lnk`（约 30 处，分布于 `MuMain/out`、`OpenMU/artifacts`、`发布包`、`OpenMU-Android/server-build*`）
> 这两个名字是**韩文原名被 GBK 误解码**产生的乱码，游戏按名字加载资源，重命名会打断 Object34 / World34 的加载。

| 现名 | 建议新名 |
| --- | --- |
| `发布包/` | `release/` |
| `OpenMU-数值重设计/` | `OpenMU-NumericRedesign/` |
| `OpenMU-安卓手机版-可安装/` | `OpenMU-Android-Installable/` |
| `…/OpenMU-手机服务器/` | `…/OpenMU-MobileServer/` |
| `移动端完整性审计报告.md` | `Mobile-Integrity-Audit-Report.md` |
| `Glace678-3-代码审查报告.md` | `Glace678-3-Code-Review-Report.md` |
| `AI_交接清单_2026-09-27.md` | `AI-Handover-2026-09-27.md` |
| `AI_交接清单_2026-09-27_晚间版.md` | `AI-Handover-2026-09-27-Evening.md` |
| `OpenMU-数值重设计/docs/数值设计总案.md` | `…/docs/numeric-design-master-plan.md` |
| `OpenMU-数值重设计/docs/接入清单.md` | `…/docs/integration-checklist.md` |
| `OpenMU-数值重设计/docs/调参记录.md` | `…/docs/tuning-log.md` |
| `…/artifacts/验证报告.md`（2 处） | `…/artifacts/verification-report.md` |
| `OpenMU-HarmonyOS/BUILD-鸿蒙构建说明.md` | `BUILD-HarmonyOS.md` |
| `OpenMU-iOS/BUILD-iOS构建说明.md` | `BUILD-iOS.md` |
| `发布包/使用说明.txt` | `release/README.txt` |
| `发布包/Server/README-简体中文.txt` | `README-zh-CN.txt` |
| `OpenMU-安卓手机版-可安装/使用说明.md` | `README.md` |
| `…/OpenMU-手机服务器/README-简体中文.txt` | `README-zh-CN.txt` |
| `OpenMU/artifacts/final/OpenMU-Local/README-简体中文.txt` | `README-zh-CN.txt` |
| `OpenMU-Android/server-build*/OpenMU-Local/README-简体中文.txt`（2 处） | `README-zh-CN.txt` |
| `ui-repair/修复记录.md` | 随 `ui-repair/` 一并删除，无需改名 |

**重命名后必须同步更新的引用（否则构建会断）**

| 文件 | 行 | 内容 |
| --- | --- | --- |
| `config.py` | 39 | `PUBLISH = os.path.join(WORKSPACE, '发布包')` → `'release'` |
| `OpenMU-Android/Build-AndroidPackage.ps1` | 4 | `'..\OpenMU-安卓手机版-可安装'` → `'..\OpenMU-Android-Installable'` |
| `OpenMU-Android/Build-AndroidPackage.ps1` | 264 | `'OpenMU-手机服务器'` → `'OpenMU-MobileServer'` |
| `OpenMU/src/GameLogic/BalanceV1.cs` | 40 | 注释中的 `OpenMU-数值重设计/design/balance.v1.json` |
| `.gitignore` | — | `/发布包/Server/`、`/发布包/Game/`、`/OpenMU-安卓手机版-可安装/OpenMU-手机服务器/`、`*.apk` 等条目 |

### 2.3 源码中文内容清单（待核查，暂不修改）

扫描口径：源码/文档/配置（`.cs .cpp .h .py .ps1 .js .md .json .xml .resx .gradle .java .kt .sh .yml .html .txt .cmake`），已排除 `obj/ bin/ build/ out/ artifacts/ .git/ node_modules/`。
结果：**391 个文件含中文**，合计 418,870 行（其中 6 份 librime 拼音词库各占 65,122 行）。

**其中的本地化数据（刻意保留，不建议英文化）**

| 类别 | 代表文件 |
| --- | --- |
| 游戏语言包 | `MuMain/src/Localization/Game.zh-CN.resx`(3282)、`Game.zh-TW.resx`(3250)、`Dialog.zh-CN.resx`(233) |
| 服务端语言包 | `OpenMU/src/DataModel/Properties/ModelResources.zh-CN.resx`(980)、`OpenMU/src/Web/AdminPanel/Properties/Resources.zh-CN.resx`(279) |
| 翻译词表 | `OpenMU/tools/localization/model-caption-translations.json`(813)、`zh-cn-translations.json`(379)、`MuMain/tools/Localization/zh-cn-overrides.json`(556)、`mainland-context-terms.json`(82) |
| 拼音词库 | `MuMain/src/third_party/librime/share/pinyin_simp.dict.yaml` 等 6 份 |
| 本地化工作区 | `loc/`(56 文件)、`_locwork/`(145)、`_zhwork/`(25) |

**建议英文化的源码文件（按模块）**

| 模块 | 文件数 | 重点文件（中文行数） |
| --- | --- | --- |
| `MuMain/src/source` | 10 | `UI/Windows/PasswordServiceWin.cpp`(33)、`World/GameMaps/GM_Kanturu_2nd.h`(4)、`UI/ControllerKeyboard/ControllerKeyboard.cpp`(3) |
| `MuMain/tests` | 3 | `test_text_line_wrap.cpp`(22)、`test_server_notice_localization.cpp`(11) |
| `MuMain/docs` | 2 | `mcp-android-drag-verification.md`(54) |
| `MuMain/src/ResxGen` | 1 | `CppEmitter.cs`(3) |
| `OpenMU/src/LocalLauncher` | 17 | `LauncherForm.cs`(45)、`PostgreSqlExecutionPaths.cs`(17)、`OpenMuServerManager.cs`(15)、`PackageManifestValidator.cs`(13) |
| `OpenMU/src/Web` | 4 | `MobileGmService.cs`(39)、`player-portal/index.html`(2) |
| `OpenMU/src/DesktopLauncher` | 1 | `LauncherWindow.cs`(17) |
| `OpenMU/tools` | 8 | `local-package/Build-LocalPackage.ps1`(5)、`local-package/README-zh-CN.txt`(36)、`BalanceLab/Program.cs`(17) |
| `OpenMU/tests` | 5 | `LocalizedStringTests.cs`(8) |
| `OpenMU/deploy` | 1 | `README.md`(97) |
| `OpenMU-Android/*/src` | 11 | `game-app/res/values/strings.xml`(45)、`values-ja/mobile_strings.xml`(40)、`gm-app/res/values/strings.xml`(54)、`MobileGmApiClient.java`(14) |
| `OpenMU-Android/`（根） | 3 | `MOBILE-GM-IMPLEMENTATION-REPORT.md`(21)、`README-zh-CN.md`(38)、`Build-AndroidPackage.ps1`(6) |
| `OpenMU-HarmonyOS/` | 7 | `BUILD-鸿蒙构建说明.md`(97)、`README-zh-CN.md`(77)、3 份 `string.json` |
| `OpenMU-iOS/` | 5 | `build-ios.sh`(10)、`game-ios/CMakeLists.txt`(11)、`build-clientlibrary-ios.sh`(17)、`README-zh-CN.md`(34) |
| `OpenMU-数值重设计/` | 6 | `docs/数值设计总案.md`(144)、`docs/接入清单.md`(85)、`README.md`(64)、`docs/调参记录.md`(30)、`balance.v1.json`(21) |
| 根目录脚本/文档 | 12 | `AUDIT_REPORT.md`(204)、`Glace678-3-代码审查报告.md`(247)、`AI_交接清单_*晚间版.md`(214)、`移动端完整性审计报告.md`(82)、`README.md`(12)、`wakeup_claude.py`(49) |
| `.github/` | 16 | `zapier-review/COST-RESEARCH.md`(50)、`README.md`(35)、`VALIDATION.md`(35) |
| `zapier-proxy/` | 3 | `zapier-client.mjs` 等（与 MU 项目无关） |

> 未改动任何源码内容，以上仅为核查清单。

### 2.4 其它待确认项

- `zapier-proxy/`（8134 文件 / 89.8 MB）+ `zapier-complete-guide.html` + `zapier-minimum-cost-research.html` + `.github/zapier-review/`：**与 MU 项目完全无关**，建议整体移出或删除。
- `local-credentials.ps1`、`provision_secrets.ps1`：可能含凭据，未做任何处理。
- `_codex-repair/`（705 文件 / 1484 MB）、`acceptance/`（177 文件 / 191.6 MB）：一次性调试工作区，本次保守档未包含。
- 构建产物合计约 30 GB（`MuMain/out` 18.9 GB、`OpenMU/artifacts` 4.2 GB、`MuMain/.nuget` 2.2 GB、`OpenMU-Android/*/build` 6.5 GB），可重建。
