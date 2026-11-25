# OpenMU-iOS（预览脚手架）

MU Online Solo 的 iOS 客户端工程。**当前状态：脚手架完整、真机移植进行中**——
引擎入口、CMake iOS 分支、Xcode 工程生成脚本、协议库构建脚本与打包约定均已就位，
但渲染层（固定功能桌面 GL → GLES2/Metal）与触摸输入尚未在真机验证。

## 目录结构

```
OpenMU-iOS/
├── build-ios.sh                  一键生成 Xcode 工程并构建 OpenMU-Game.app
├── game-ios/
│   ├── CMakeLists.txt            壳工程：挂接 MuMain/src，配置 bundle 与签名
│   ├── Info.plist.in             横屏、arm64、iOS 15+、免加密声明
│   ├── resources/game-data.zip   （构建时放入，不入库；上游 data 发布解出）
│   └── native/                   协议库产物目录（build-clientlibrary-ios.sh 输出）
└── nativeaot-ios/
    ├── build-clientlibrary-ios.sh  .NET 协议库 → libMUnique.Client.Library.dylib
    └── ClientLibrary.IosAot.props  install_name=@rpath 链接参数
```

## 快速开始（macOS）

前置：Xcode 16+（iOS 15+ SDK）、CMake ≥3.25、.NET SDK 10、Apple 开发者账号（真机）。

```bash
# 1) 协议库（实验路线，详见脚本内注释）
dotnet workload install ios          # 如未安装
./nativeaot-ios/build-clientlibrary-ios.sh

# 2) 游戏数据：从上游 sven-n/MuMain 的 data-<id> 发布解出 Data/ 与 fonts/，
#    压成 game-data.zip 放到 game-ios/resources/（首启动解包到 Documents/Data）

# 3) 生成并构建
DEVELOPMENT_TEAM=<你的团队ID> ./build-ios.sh
# 产物: build/ios/Release-iphoneos/OpenMU-Game.app
```

## GM 系统（iOS 路线）

iOS 端 GM 不单独出原生 App：管理面板是响应式 Blazor 网页，Safari 直接使用，
或"添加到主屏幕"作为 PWA；移动端 GM API（`/api/mobile-gm`）与安卓/鸿蒙原生
GM App 共用，未来若需要 SwiftUI 原生 GM App 可直接复用该 API。

## 与安卓/鸿蒙版的对应关系

| 组件 | Android | HarmonyOS | iOS |
|---|---|---|---|
| 引擎入口 | `Platform/Android/main.cpp` | `Platform/HarmonyOS/main.cpp` | `Platform/iOS/main.mm` |
| 工程 | `OpenMU-Android/`（Gradle） | `OpenMU-HarmonyOS/`（hvigor ×3） | `OpenMU-iOS/`（CMake→Xcode） |
| 渲染 | gl4es→GLES | gl4es→GLES | gl4es→GLES2 或 Metal（**待验证**） |
| 协议库 | NativeAOT linux-bionic-arm64 | NativeAOT linux-ohos/bionic-arm64 | NativeAOT ios-arm64（**实验**） |
| 触控 | MobileGestureMapper（共享） | 同左 | 同左 |
| GM App | 原生 Java | 原生 ArkTS | Web/PWA（API 同源） |

剩余移植清单与风险详见 `BUILD-iOS构建说明.md`。
