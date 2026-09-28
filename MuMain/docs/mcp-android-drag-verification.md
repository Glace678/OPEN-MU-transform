# Android 背包拖拽修复 - 模拟器实测验收记录

日期：2026-09-27（由远程 MCP 代理执行并记录）

## 结论

`CNewUISkillList::IsSelectionOpen()` 只返回 `m_bSkillList` 的修复，在 Android 35 x86_64
模拟器（ARM64 转译）中实测通过：背包首格物品按住拖动到空格后成功换位，退出重登后
位置保持，证明服务端已确认该移动。修复前该触摸会被可见 UI 枚举值 56（技能列表）拒绝。

## 测试环境

- 模拟器：AVD `OpenMU_ASCII35`（Android 35，x86_64 + ARM64 转译，2400x1080 横屏）
- 客户端：`net.munique.openmu.game.debug`，原生库热替换为
  `MuMain/out/build/android-arm64-release/src/libmain.so`
  （SHA256 `3cacdee67c77d79891c71b06d8555ca4ac9a663524ef3cbe6092123a1a706693`）
- 被替换前的设备库已备份：`acceptance/android-device-libmain-backup-20260927-1840.so`
- 服务端：本机运行中的手机服务器实例（PostgreSQL 55432，ConnectServer 44406）
- 链路：App -> 10.0.2.2:44416 -> `acceptance/android-demo-proxy-local1.cjs` -> 127.0.0.1:44406，
  游戏服重定向经 `adb reverse tcp:55901->45901, tcp:55902->45902`
- 测试账号：`dragtest`（经 `/api/registration/create` 全新注册，未触碰任何既有账号）
- 测试角色：`DragElf1`（弓箭手，1 级，出生 Noria/仙踪林 173,112）

## 步骤与结果

1. 注册 dragtest -> 客户端登录成功（服务器 Valhalla-1）。
2. 创建弓箭手 DragElf1，Connect 进入 Noria，打开背包（I、V）。
3. 背包初始：第 1 行第 1、2 格各一件物品（首格为"战士之戒"）。
4. 按住首格物品拖到第 2 行第 1 格（swipe 900ms）：视觉换位成功，
   tooltip 在新位置正常显示物品信息。截图：`acceptance/drag-fix-after-drag-20260927.png`。
5. force-stop 客户端并完整重登、重新进入世界后开背包：首格为空、
   戒指仍在第 2 行第 1 格。截图：`acceptance/drag-fix-after-relogin-20260927.png`。
   服务端持久化确认通过。

## 尚未覆盖（后续工作）

- 交接清单 1.3 的扩展手势矩阵：单击仅查看、按住 500ms 使用、不可用物品防误操作、
  拖出背包恢复、双指/失焦取消、Q/W/E/R 药水格互斥 —— 未逐项执行。
- `OpenMU-Android/native/arm64-v8a/libmain.so` 与 APK 仍是旧版本，
  正式 APK 重建与安装复测（清单 1.4）未做。
- 模拟器为 ARM64 转译，不能替代真机性能与手感验收（清单 7）。
- 本次为验证方便使用了本机运行中的正式服务器 + 全新注册账号；
  未修改任何既有账号/角色数据。演示服务器（-demo 内存实例）初始化耗时约
  15-25 分钟，两次尝试均被放弃，原因待查（可能与 balance-v1 校验循环有关）。

## 证据文件

- `acceptance/drag-fix-after-drag-20260927.png`（拖拽后）
- `acceptance/drag-fix-after-relogin-20260927.png`（重登后）
- `acceptance/android-device-libmain-backup-20260927-1840.so`（原设备库备份）
- `acceptance/android-demo-proxy-local1.cjs` / `.log`（代理与协议观测）

## 追加：正式 APK 重建与回归（同日）

1. 修复库同步到 `OpenMU-Android/native/arm64-v8a/libmain.so`（SHA256 与源构建一致）。
2. `gradlew :game-app:assembleDebug -POPENMU_SERVER_ADDRESS=192.168.215.56
   -POPENMU_MOBILE_PACKAGE_KEY=<mobile-server-settings.json 中的 MobilePackageKey>`
   构建成功（APK 约 1.07 GB，资源内置）。
3. 模拟器 /data 空间不足以覆盖安装，先备份应用偏好
   （`acceptance/openmu-mobile-prefs-backup.xml`）后卸载重装，装后经 run-as 恢复偏好。
4. 安装后校验 `/data/app/.../lib/arm64/libmain.so` SHA256 = `3cacdee6...`，
   证明修复库随 APK 正式分发，不再依赖热替换。
5. 回归：dragtest/DragElf1 登录进入 Noria，打开背包，把战士之戒从第 2 行第 1 格
   拖回第 1 行第 1 格，视觉换位成功（物品同时恢复到初始布局）。
   截图：`acceptance/apk-rebuild-drag-regression-20260927.png`。

## 追加：入口页 UI 暗黑风格重设计（同日）

`game-app/src/main/java/net/munique/openmu/game/BootstrapActivity.java`：
深色渐变背景（近黑到暗绯）、金色品牌标题（加粗+字距）、状态文字阴影、
圆角渐变按钮（主按钮猩红渐变+金描边，次按钮暗钢渐变+浅描边）、进度条绯红着色。
截图：`acceptance/ui-entry-dark-redesign-20260927.png`。
后续可选：SettingsActivity 与手势教程弹窗按同一风格统一。
