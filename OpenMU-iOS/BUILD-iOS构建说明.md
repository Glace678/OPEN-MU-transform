# iOS 构建与交接说明

本目录提供 **CMake 生成的 Xcode 工程脚手架**。当前机器环境没有 macOS/Xcode，
因此这里不产出 `.app`/`.ipa`；在装好 Xcode 的 Mac 上按下面步骤即可开始真机移植。
原生 C++ 侧的 iOS 适配（`__APPLE__`+`CMAKE_SYSTEM_NAME=iOS` 分支、平台入口、
`MU_IOS_PREVIEW` 开关）已写好。

## 一、准备工具链

1. **Xcode 16+**，安装 iOS 15+ SDK；命令行工具 `xcode-select --install`。
2. **CMake ≥ 3.25**（brew install cmake）。
3. **.NET SDK 10** + iOS workload（`dotnet workload install ios`）。
4. Apple 开发者账号（真机部署与签名；模拟器可免）。

## 二、构建步骤

```bash
cd OpenMU-iOS
# 协议库（.NET → 原生 dylib，实验路线，脚本内有失败处置指引）
./nativeaot-ios/build-clientlibrary-ios.sh
# 游戏数据 zip（来自上游 sven-n/MuMain data 发布，解出 Data/+fonts/ 后打包）
cp /path/to/game-data.zip game-ios/resources/
# 生成 Xcode 工程并构建真机包
DEVELOPMENT_TEAM=XXXXXXXXXX ./build-ios.sh
```

产物：`build/ios/Release-iphoneos/OpenMU-Game.app`；用 Xcode 打开
`build/ios/OpenMUiOS.xcodeproj` 可直接部署调试。

## 三、首启动数据流（约定已实现，解包逻辑待接）

- `game-data.zip` 作为 bundle 资源打包（wrapper CMakeLists 已声明资源拷贝）。
- 引擎入口 `MuMain/src/source/App/Platform/iOS/main.mm`：优先读环境变量
  `MU_IOS_DATA_ROOT`，否则回退 `<Documents>/Data`；存在则 `chdir` 并设置
  `MU_CONFIG_FILE`。**zip → Documents/Data 的首启动解包**需在壳层补一段
  Objective-C++（或首版直接用 Xcode 把解包好的 Data/ 拷进 bundle 资源）。

## 四、剩余移植清单（按依赖排序）

| # | 任务 | 说明 |
|---|---|---|
| 1 | 渲染层验证 | 引擎为固定功能桌面 GL：走 gl4es→GLES2（与安卓/鸿蒙同源，`MU_IOS_GL4ES_ROOT` 注入）或评估 SDL GPU Metal 后端；EAGL 上下文由 SDL3 iOS 后端管理 |
| 2 | 协议库加载 | iOS 限制 dlopen 任意路径：产物放 `OpenMU-Game.app/Frameworks/`，`Connection` 的加载路径按 `@rpath/libMUnique.Client.Library.dylib` 适配（props 已写 install_name） |
| 3 | OBJCXX 语言启用核对 | macOS 桌面路径已能编译 .mm；iOS 工具链下核对 CMake 语言启用与 `main.mm` 的 SDL_main 宏链路 |
| 4 | 触摸与手势 | MobileGestureMapper 与安卓共享（SDL_HINT_TOUCH_MOUSE_EVENTS 已在入口关闭合成）；核对 SDL3 iOS 的 finger 事件坐标与 SafeArea |
| 5 | 中文输入 | iOS 用系统键盘（SDL3 screen keyboard / UIKit first responder），不走 Rime |
| 6 | 高刷 | ProMotion 120Hz：Info.plist 增加 `CADisableMinimumFrameDurationOnPhone`，配合引擎 FrameRateMode |
| 7 | 签名分发 | DEVELOPMENT_TEAM 自动签名；TestFlight/App Store 需要完整签名与隐私清单（PrivacyInfo.xcprivacy） |

## 五、诚实声明

- 本脚手架**未在任何真机/模拟器上编译验证**（编写环境无 Xcode）；每一步都有
  明确报错指引，遇到问题按第四节顺序排查。
- .NET NativeAOT 的 ios-arm64 目标为实验性质，脚本失败时给出的替代路线
  （.NET for iOS workload 静态库）需要同步改 `Connection` 的加载方式。
- 与安卓版共享的 MobileGestureMapper / 帧率系统 / 简中资源均有 CI 测试背书，
  iOS 侧继承这些实现，风险集中在渲染与打包链路。
