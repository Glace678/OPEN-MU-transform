# OpenMU 鸿蒙（HarmonyOS / OpenHarmony）移植版

构建检查会读取真正的 JSON5 配置，验证基础配置及构建模式覆盖项。游戏包默认地址
必须为 localhost 或私网 IPv4；GM 的 `DEFAULT_SERVER_URL` 在公网使用 HTTPS。
缺少配对密钥或地址时会停止构建，避免产生启动后才失败的安装包。
`OPENMU_ALLOW_PLACEHOLDER_MOBILE_KEY=1` 仅允许本地联调占位密钥，不会关闭地址校验。
共享校验代码位于 `build-tools`，每个项目的 hvigor 配置固定使用 JSON5 2.2.3。
无需 DevEco 的回归检查可运行 `npm ci --prefix build-tools`，然后
`npm test --prefix build-tools`。此检查不替代 HAP 原生编译和设备验证。

本目录是 OpenMU 在鸿蒙系统上的移植工程，覆盖三类设备形态：

| 工程 | 目标设备 | 形态 |
| --- | --- | --- |
| `harmony-game/` | 华为手机、折叠屏、平板（HarmonyOS 4 / 5，API 12+） | 触屏游戏 APP（.hap） |
| `harmony-gm/` | 同上 | 原生 ArkTS 手机 GM APP（免登录、发装备/道具/金币） |
| `harmony-pc/` | 鸿蒙电脑（HarmonyOS PC / 2-in-1，API 14+） | 桌面窗口游戏，键鼠为主、触屏为辅 |

三者共用同一份 C++ 游戏客户端（`MuMain/src/source`）与同一个 .NET NativeAOT 协议库
（`MuMain/ClientLibrary`），只是平台壳不同：安卓用 SDL 的 Android 后端 + Java 壳，
鸿蒙用 SDL 的 OpenHarmony 后端 + ArkTS/NAPI 壳。

---

## 1. 架构与复用关系

```
                ┌─────────────────────────────────────────────┐
                │   共享原生核心（不改业务逻辑，直接复用）        │
                │                                             │
  MuMain/src/source  ── C++ 客户端：渲染(OpenGL ES)、音频、      │
  (App/Platform/*, Core/Input/MobileGestureMapper.*, …)         │
  │                手势映射 MobileGestureMapper（纯 C++，        │
  │                与平台无关，安卓/鸿蒙共用同一套手势判定）      │
  │                                                           │
  MuMain/ClientLibrary ── .NET NativeAOT → 协议库 .so           │
  │                安卓: linux-bionic-arm64                     │
  │                鸿蒙: linux-ohos-arm64（见 nativeaot-ohos）  │
  └───────────────┬──────────────────────┬─────────────────────┘
                  │                      │
        SDL3 OpenHarmony 后端     SDL3 Android 后端
        (libsdl/SDL ohos 分支)     (已内置 ThirdParty/SDL)
                  │                      │
        ┌─────────┴────────┐    ┌────────┴─────────┐
        │ harmony-game     │    │ OpenMU-Android    │
        │ harmony-pc (ArkTS)│    │ (Java 壳，已完成) │
        └──────────────────┘    └──────────────────┘
```

关键点：

- **手势/操作零重写**：`Core/Input/MobileGestureMapper.{h,cpp}` 不依赖任何 Android API，
  鸿蒙 NAPI 层把 `TouchEvent` 归一化成同样的 `TouchSample` 喂给它，输出同样的
  `MobileGestureAction`（移动、左右键、切技能、镜头缩放、地图、设置）。因此安卓上的
  「左半屏拖动移动 / 右半屏点击确认 / 右半屏双击技能 / 双指横滑切技能 / 双指竖滑或捏合拉镜头 /
  三指上滑地图 / 三指下滑设置 / 左手模式」在鸿蒙上行为完全一致。
- **免登录零重写**：由包密钥 HMAC-SHA256 派生固定本地账号，通过
  `MU_LOCAL_AUTO_LOGIN` 等环境变量在原生线程启动前注入（与安卓相同）。
- **GM 接口零重写**：服务端 `/api/mobile-gm/*` 不变；鸿蒙 GM 用 ArkTS 的
  `@ohos.net.http` 实现等价客户端，携带同样的 `X-OpenMU-Mobile-Key`。

---

## 2. 构建前的外部依赖（重要）

本仓库提供的是**完整可导入 DevEco Studio 的工程源码与构建脚本**，但鸿蒙原生编译需要
华为工具链，无法在纯命令行/无 DevEco 环境产出 .hap。请在一台安装好以下工具的机器上构建：

1. **DevEco Studio**（6.0 或更高，带 HarmonyOS SDK / Native（C/C++）开发组件）。
2. **HarmonyOS SDK**：API 12（手机/平板/折叠屏）或 API 14（鸿蒙电脑）；含
   `native`（clang/lld for arm64-v8a / x86_64）与 `ets-loader`、`hvigor`。
3. **SDL3 的 OpenHarmony 后端**：上游 SDL3 已含 `android-project`，OpenHarmony 后端在
   SDL 官方/社区的 `ohos` 移植中（`SDL/src/core/ohos/`、`SDL/ohos-proj/`）。
   将其放入 `MuMain/src/ThirdParty/SDL/`（与 android-project 并列的 `ohos-proj/`），
   或在 `harmony-game/entry/src/main/cpp/CMakeLists.txt` 中把 `SDL3` 指向你的 SDL OHOS 构建。
4. **.NET NativeAOT 的 OpenHarmony 运行时包**：协议库默认 RID 是 `linux-bionic-arm64`。
   鸿蒙使用 musl（bionic 近亲）+ OHOS libc。使用 `nativeaot-ohos/` 下的脚本，配合
   .NET 10 的 OHOS crossrootfs（`Microsoft.NETCore.App.Runtime.linux-ohos-arm64` 或自管
   sysroot）以 `linux-ohos-arm64` 发布 `libMUnique.Client.Library.so`。若官方尚未发布该
   RID 的运行时包，脚本会指引你用 OHOS NDK sysroot 生成 crossrootfs。

> 说明：当前 Windows 工作机没有 DevEco Studio / HarmonyOS NDK，因此这里不产出 .hap；
> 工程源码、CMake、NAPI 桥、ArkTS 界面与构建脚本均已就绪，在装好上述工具链的机器上
> 用 DevEco Studio「Open Folder → harmony-game」即可 Sync + Build。

---

## 3. 目录说明

- `harmony-game/`：手机/折叠屏/平板游戏 APP。
  - `entry/src/main/ets/`：ArkTS 界面（启动解压、设置、手势教程、XComponent 承载原生渲染）。
  - `entry/src/main/cpp/`：NAPI 桥 + CMake，编译 MuMain 原生库与 SDL OHOS 后端。
- `harmony-gm/`：手机 GM APP（纯 ArkTS，无原生库）。
- `harmony-pc/`：鸿蒙电脑版（桌面窗口、键鼠、自由分辨率、手柄）。
- `nativeaot-ohos/`：把 `MuMain/ClientLibrary` 以 OpenHarmony 为目标发布 NativeAOT .so 的脚本。
- `shared/`：三端共用的手势说明文案与适配常量。

## 4. 设备适配（折叠屏 / 平板 / 手机 / 电脑）

- 使用 ArkTS **栅格断点** `sm / md / lg`（依据宽度 vp）切换布局：手机竖屏单列、
  平板/折叠展开双列、折叠悬停态半屏。
- XComponent 用 **FILL** 拉伸铺满；原生侧以真实像素宽高比设置 3D 相机，HUD 以
  640×480 参考分辨率等比缩放（与安卓同一套 `g_fScreenRate_x/y` + 安全区逻辑）。
- 折叠屏：监听 `onFoldStatusChange`（展开/悬停/折叠）与 `onWindowSizeChange`，
  在折痕处用 `display.on('foldStatus')` + 安全区避让，避免 HUD 压到折痕/刘海/圆角。
- 平板：支持横屏左右分栏（左侧移动区更大），UI 缩放按 `vp` 密度自适应。
- 鸿蒙电脑：默认窗口 1280×720、可缩放、鼠标指针常驻、键盘直达，复用桌面 Win32 版的
  键鼠输入路径（平台宏 `__OHOS__` 下走 SDL 鼠标/键盘事件）。

## 5. 与安卓版一致的局域网/安全模型

游戏与 GM 连接同一台电脑上运行的 OpenMU 本地服务器（Windows `OpenMU-Local.exe`）。
手机/平板/鸿蒙电脑与电脑连同一可信 Wi-Fi；APP 里填电脑 IPv4（游戏端口 `44406`，
GM 地址 `http://电脑IP:5080`）。包密钥随安装包派生本地账号，请勿把端口暴露到公网。
