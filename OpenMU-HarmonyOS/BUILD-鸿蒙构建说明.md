# 鸿蒙版构建与交接说明

本目录提供三套**可直接导入 DevEco Studio 的工程源码**。当前 Windows 工作机未安装
DevEco Studio / HarmonyOS NDK，因此这里不产出 `.hap`；在装好华为工具链的机器上按下面
步骤即可编译。原生 C++ 侧的鸿蒙适配（`__OHOS__` 宏、平台入口、CMake 平台分支）与
ArkTS 壳均已写好。

## 工程一览

| 目录 | 内容 | 原生依赖 |
| --- | --- | --- |
| `harmony-game/` | 手机/折叠屏/平板游戏 APP（API 12，横屏，触屏手势） | libmain.so(SDL+MuMain)、libSDL3.so、libGL.so(gl4es)、libMUnique.Client.Library.so、libopenmubridge.so |
| `harmony-gm/` | 手机 GM APP（纯 ArkTS，无原生库），发装备/道具/金币 | 无 |
| `harmony-pc/` | 鸿蒙电脑/二合一（API 14，窗口化，键鼠为主，arm64+x86_64） | 同 harmony-game |

## 一、准备工具链

1. 安装 **DevEco Studio 6+**，在 SDK Manager 中安装：
   - 手机/平板：`HarmonyOS 5.0.0(12)` 的 **Native (C/C++)** 与 ArkTS/ETS SDK；
   - 鸿蒙电脑：再装 `HarmonyOS 5.0.x(14)`（含 2in1/desktop 设备形态）。
2. 命令行构建可用 `hvigorw`（DevEco 自带）。

## 二、准备原生库（游戏/电脑版需要，GM 版跳过）

原生游戏由三部分组成，与安卓版同源：

1. **SDL3 的 OpenHarmony 后端**
   上游 SDL3 主线已含 OpenHarmony 支持（`SDL/src/core/ohos/`、`SDL/ohos-proj/`，
   napi 模块名 `SDL3`，正是 XComponent `libraryname: 'SDL3'` 对应的库）。
   - 把含 OHOS 后端的 SDL3 放入 `MuMain/src/ThirdParty/SDL/`（与 `android-project`
     并列的 `ohos-proj/`），MuMain 的 CMake 会像安卓一样 `add_subdirectory(SDL)` 产出
     `libSDL3.so`。
2. **gl4es（桌面 GL → GLES）**
   游戏引擎用固定功能桌面 GL，安卓经 gl4es 翻译到 GLES。鸿蒙同理。
   - 为 OHOS 编译 gl4es（用 OHOS NDK clang），把源码根目录路径传给 CMake：
     `-DMU_OHOS_GL4ES_ROOT=<gl4es 源码路径>`（CMake 会 `add_subdirectory` 它）。
3. **.NET 协议库（NativeAOT）**
   运行 `nativeaot-ohos/build-clientlibrary-ohos.sh`（需在 Linux/WSL + OHOS NDK 环境）：
   - 设置 `OHOS_NDK_HOME` 指向鸿蒙 NDK；
   - 优先用 RID `linux-ohos-arm64`；若该 .NET 预览版未提供 OHOS 运行时包，脚本会提示
     改用 `OPENMU_OHOS_RID=linux-bionic-arm64` 配合 OHOS sysroot；
   - 产物 `libMUnique.Client.Library.so` 会拷到
     `harmony-game/entry/src/main/cpp/prebuilt/arm64-v8a/`（电脑版 x86_64 放
     `harmony-pc/.../prebuilt/x86_64/`）。

> 这三个库与安卓版 `OpenMU-Android/native/arm64-v8a/` 里的对应物一一同源，只是目标 ABI
> 换成 OHOS。MuMain 的 `src/CMakeLists.txt` 已新增 `OHOS` 平台分支（共享 SDL、gl4es、
> EGL/GLES、hilog、`__OHOS__` 编译定义）。
>
> **libcurl（可选）**：游戏内商城的商品目录/补丁下载在非 Windows 平台用 libcurl
> （`CurlFileDownloader`）。OHOS NDK 不带 libcurl，CMake 不会因此报错（与安卓一致，
> 仅 warning）。若需要商城下载功能，用 OHOS NDK 编一份 libcurl，再传
> `-DMU_OHOS_CURL_INCLUDE_DIR=<含 curl/curl.h 的目录>` 与
> `-DMU_OHOS_CURL_LIBRARY=<libcurl.so 路径>`；不提供则只有商城下载不可用，游戏本体正常。
>
> **数据目录首启自愈**：ArkTS 壳在解压数据包后、进入游戏前会再次把原生进程的工作目录
> 指向沙盒 `filesDir/game-data`（`EntryAbility.onCreate` 在首启解压之前先设过一次，那时
> 目录还不存在），无需手工干预；`MU_CONFIG_FILE` 也会随之指向该目录的 `config.ini`。
>
> 路径说明：`entry/src/main/cpp/CMakeLists.txt` 默认把 `MU_REPOSITORY_ROOT` 解析为与
> `OpenMU-HarmonyOS/` 同级的 `MuMain/`（即本仓库布局），并会在找不到时直接报致命错误；
> 若 MuMain 在别处，在 `entry/build-profile.json5` 的 `externalNativeOptions.arguments`
> 里加**绝对路径** `-DMU_REPOSITORY_ROOT=D:/your/path/MuMain`（相对路径按 cpp 目录解析，
> 不要用）。gl4es 源码路径同理用 `-DMU_OHOS_GL4ES_ROOT=<绝对路径>` 传入。

## 三、导入与编译

1. DevEco Studio → **Open Project** → 选择 `harmony-game/`（或 `harmony-gm/`、`harmony-pc/`）。
   三套工程都已自带 hvigor 脚手架（根目录与 `entry/` 的 `hvigorfile.ts`、
   `hvigor/hvigor-config.json5`、`entry/oh-package.json5`）；若 DevEco 提示
   hvigor 版本更新，按提示升级即可，命令行也可直接用工程根目录的 `hvigorw`
   （首次同步时 IDE 会自动生成 wrapper 脚本）。
2. File → Project Structure → 配置签名（调试证书/自动签名）。
3. 放入游戏数据：把与安卓相同的 `game-data.zip`（约 740 MB，含 `Data/`、`fonts/`）拷到
   `entry/src/main/resources/rawfile/game-data.zip`。首次启动会自动解压到沙盒
   `filesDir/game-data`。GM 版不需要数据包。
4. 校对配对密钥与服务器地址（**只需改 build-profile，ArkTS 代码经 `BuildProfile`
   自动读取，不要再硬编码**）：
   - `entry/build-profile.json5` 的 `buildProfileFields.MOBILE_PACKAGE_KEY` 必须与
     游戏、服务器使用同一把 43 位 base64url 密钥（当前是占位的 43 个 `A`，仅调试用）；
   - 游戏/电脑版改 `DEFAULT_SERVER_ADDRESS`（电脑局域网 IPv4，端口固定 44406），
     GM 版改 `DEFAULT_SERVER_URL`（形如 `http://192.168.x.x:5080`）；
   - 游戏内"设置"页写出的 `config.ini` 使用与桌面版一致的段名/键名
     （`[CONNECTION SETTINGS] ServerIP/ServerPort`、`[Render] FrameRateMode/...`、
     `[Input] Handedness/...`），以合并方式写入数据包自带的 config.ini，
     手机端只覆盖自己管理的键，其余（音量、登录等）保留。
5. 点 **Run** 或 `hvigorw assembleHap`。

## 四、设备适配要点（已在代码中实现）

- **栅格断点**：GM/设置页用 `GridRow` 的 `sm/md/lg`（600vp/840vp），手机单列、折叠展开/
  平板居中限宽双列。
- **折叠屏**：游戏页 `display.on('foldStatusChange' | 'change')` 监听展开/悬停/折叠与尺寸
  变化；XComponent 铺满，原生侧按真实宽高比重设相机（640×480 参考分辨率等比缩放 +
  安全区），折痕/圆角/刘海避让。
- **平板/二合一**：GM 表单居中限宽；游戏支持横屏；电脑版自由窗口（最小 960×600），
  键鼠为主，触屏二合一仍走同一套手势。
- **手势零重写**：`MuMain/src/source/Core/Input/MobileGestureMapper.{h,cpp}` 是纯 C++，
  安卓与鸿蒙共用；OHOS SDL 后端把多指触摸交给 SDL，主循环映射为
  移动/点击/双击技能/双指切技能·缩放/三指地图·设置。
- **免登录零重写**：ArkTS 用与安卓 `MobileIdentity.java` 逐字节一致的 HMAC-SHA256
  （自包含 SHA-256 实现，见 `ets/mobile/MobileIdentity.ets`）派生账号，经 NAPI 桥
  `libopenmubridge.so` 在 SDL 线程启动前注入 `MU_LOCAL_AUTO_LOGIN` 等环境变量。

## 五、GM 接口

鸿蒙 GM 与安卓 GM、服务器完全共用 `/api/mobile-gm/*`：
- `GET /status`（账号、游戏服务器是否已启动 `serverReady`、在线角色、等级、金币余额；
  管理服务在线但游戏服未启动时，APP 会明确提示"游戏服务器尚未启动"）
- `GET /items?q=`（物品搜索，返回能否带幸运/追加/卓越及卓越条数）
- `POST /grant-item`（group/number/level/quantity/hasSkill/**hasLuck**/
  **additionalOptionLevel**/**excellentMask**）
- `POST /grant-zen`（amount，1–20 亿）

发装备支持：等级 +0~+15、附带技能、**幸运**、**追加 +4/+8/+12/+16**、**卓越属性勾选/全选**。
所有写操作幂等（每次点击一个新 UUID）、仅对在线角色生效、服务端逐项校验。

## 六、当前未在本机验证的部分（需 DevEco/真机复验）

- ArkTS/ETS 编译与 `.hap` 打包（本机无 DevEco）；
- SDL OHOS 后端、gl4es OHOS、.NET NativeAOT OHOS 运行时包的实际链接；
- 真机 GPU 兼容、触摸手感、折叠/平板/PC 窗口的端到端表现。

C++ 侧改动均为**叠加式**：`__OHOS__` 只在鸿蒙工具链下定义，桌面（Win/Linux/macOS）与
安卓构建路径不受影响。
