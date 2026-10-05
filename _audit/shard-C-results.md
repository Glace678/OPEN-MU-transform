# Shard C 修复结果（移动平台四目录）

工作根目录：`D:\openmu自用`。独占目录：OpenMU-Android、OpenMU-HarmonyOS、OpenMU-iOS、OpenMU-安卓手机版-可安装。所有改动保持未提交，未执行 git commit/push。

本机工具情况：`gradle`/`groovy`/`cmake` 均未安装，无法执行 assemble/configure；`dotnet` 可用，`bash`(WSL) 可用但无法访问 `D:\` 路径（WSL 报 localhost 限制）。因此移动端均为**已加固 + 静态/语法校验通过，未做完整构建验证**。

| finding ID | 文件:行 | 状态 | 验证方式 |
|---|---|---|---|
| PLAT-A01 | OpenMU-Android/Open-Windows-Firewall.ps1:1 | 已修复 | PS 解析器 ParseFile 0 错误（param 已移至首行） |
| PLAT-A02 | OpenMU-Android/Build-AndroidPackage.ps1:273 起 | 已加固 | 新增 -AndroidSdkRoot/ANDROID_SDK_ROOT/local.properties sdk.dir 解析 + build-tools 回落；ParseFile 0 错误 |
| PLAT-A03 | game-app/build.gradle; gm-app/build.gradle; Build-AndroidPackage.ps1 | 已加固 | 两个 build.gradle 增加 keystore.properties/环境变量 release signingConfig；脚本新增 -BuildType；groovy 括号配平 41/41、25/25（无 groovy 运行环境，未编译） |
| PLAT-A04 | gm-app/AndroidManifest.xml; main|debug res/xml/network_security_config.xml; gm-app/build.gradle; MobileGmApiClient.java; Build-AndroidPackage.ps1 | 已加固 | 移除全局 usesCleartextTraffic；main NSC 默认禁 cleartext、仅 debug 源集放行 LAN；scheme 默认 https（构建属性注入）；Java 构造器拒绝 http→公网；3 个 XML [xml] 解析通过 |
| PLAT-A05 | Build-AndroidPackage.ps1 Test-GameDataStale | 已加固 | 改为源清单 sidecar(.sig) 比对（路径+长度+mtime），删除源文件也能检出；ParseFile 0 错误 |
| PLAT-A06 | game-app/build.gradle; gm-app/build.gradle; Build-AndroidPackage.ps1 | 已加固 | 三处统一严格 IPv4/主机名校验（空标签/首尾连字符/前导零）；groovy/PS 解析通过 |
| PLAT-H01 | harmony-game/.../cpp/CMakeLists.txt:57 | 已加固 | 强制校验 libMUnique.Client.Library.so 存在；CMake 括号配平 22/22（无 cmake 运行环境） |
| PLAT-H02 | harmony-pc/.../cpp/CMakeLists.txt:22 | 已加固 | 同上按 OHOS_ARCH 槽位校验必需 soname；括号配平 24/24 |
| PLAT-H03 | harmony-gm/.../MobileGmClient.ets; harmony-game/.../MobileIdentity.ets; build-profile.json5 | 已加固 | 密钥本即构建时注入（build-profile 仅 43×'A' 占位）；新增 GM 端占位 key 拒绝；game 端已有占位守卫保留 |
| PLAT-H04 | harmony-game/.../mobile/MobileConfig.ets:86 | 已加固 | 新增 isValidServerAddress 并在写 config.ini 前强制校验；.ets 人工核对语法（无 ohpm/DevEco） |
| PLAT-H05 | harmony-game/.../cpp/native_bridge.cpp; EntryAbility.ets | 已加固 | setenv/chdir/napi 调用逐项检查并 napi_throw_error；onCreate 记录失败、不再静默 loadContent |
| PLAT-H06 | harmony-gm/.../model/MobileGmClient.ets:58 | 已加固 | isAllowedServerUrl 双协议均拒绝 userinfo/空 host/异常 authority；构造器强制调用 |
| PLAT-H07 | nativeaot-ohos/build-clientlibrary-ohos.sh:43 | 已加固 | restore/publish 分离，publish --no-restore；人工核对 bash 语法（WSL 无法访问 D:，未跑 bash -n） |
| PLAT-I01 | OpenMU-iOS/game-ios/CMakeLists.txt:48 | 已加固 | dylib 声明 MACOSX_PACKAGE_LOCATION=Frameworks + rpath；括号配平 29/29（无 cmake） |
| PLAT-I02 | OpenMU-iOS/game-ios/CMakeLists.txt:51 | 已加固 | game-data.zip 缺失改为 FATAL_ERROR（除非 MU_IOS_ALLOW_EMPTY_DATA） |
| PLAT-I03 | OpenMU-iOS/game-ios/CMakeLists.txt | 已加固 | 强制 game-data.zip.sha256 sidecar 并 file(SHA256) 比对，不符即停止 |
| PLAT-I04 | OpenMU-iOS/nativeaot-ios/build-clientlibrary-ios.sh:23,67 | 已加固 | RID 白名单仅 ios-arm64，拷贝前 lipo -info 校验 arm64 |
| PLAT-I05 | OpenMU-iOS/build-ios.sh:14 | 已加固 | 模拟器按 uname -m 选 arm64/x86_64（MU_IOS_ARCH 可覆盖），不再固定 arm64 |
| PLAT-P01 | 可安装/Install-APKs.ps1 | 已加固 | 安装前校验 APK 存在且 SHA-256 匹配清单（实测两 APK 均 match=True） |
| PLAT-P02 | 可安装/Install-APKs.ps1; SHA256SUMS.txt | 已加固并实测 | 安装前 Get-FileHash 比对 SHA256SUMS.txt；实测 GM=79dfe8…、Game=bcdd0e… 均一致 |
| PLAT-P03 | 可安装/Open-Windows-Firewall.ps1 | 已加固 | 规则更新 try/catch + 失败回滚旧规则；ParseFile 0 错误 |
| PLAT-P04 | 可安装/Open-Windows-Firewall.ps1 | 已加固 | Private  profile + LocalSubnet，Public 网络拒绝安装（-Force 例外） |
| PLAT-P05 | 可安装/Install-APKs.ps1:24 | 已具备（原有） | adb install 后 $LASTEXITCODE 检查原本即存在，保留 |
| PLAT-P06 | 可安装/使用说明.md | 已加固 | 顶部加入调试证书/侧载/校验和提示块 |
| NEW-ANDROID-01 | nativeaot/build-clientlibrary-android.sh:32 | 已加固 | 移除无版本固定的 apt-get download unzip 回退，改为受控前置依赖、缺失即失败 |
| NEW-HARMONY-01 | nativeaot-ohos/build-clientlibrary-ohos.sh:18 | 已加固 | RID 白名单并映射到正确 ABI 槽位（arm64-v8a/x86_64），拷贝前 llvm-readelf 校验 ELF Machine |
| DEP-003 | OpenMU-Android/gradle/wrapper/gradle-wrapper.jar | 部分加固 | 实测本地 jar SHA-256=2c23278a62dc9f96ab11bee897d9f53e6479d6419135e4560ce822a6431df5e5；未替换二进制（无法在本机取得并校验官方 jar），见摘要 |
| DEP-004 | OpenMU-Android/gradle/wrapper/gradle-wrapper.properties | 已加固 | 新增 distributionSha256Sum=f8b4f4772d302c8ff580bc40d0f56e715de69b163546944f787c87abf209c961（官方 services.gradle.org 取得） |

## Shard C 修复摘要

### 关键改动
- **凭据/传输**：GM bearer 默认 HTTPS（构建属性 OPENMU_SERVER_SCHEME，默认 https），release 全局禁 cleartext、仅 debug 源集放行 LAN；Java 端拒绝 http→公网；Harmony GM 双协议拒绝 userinfo；配对密钥保持构建时注入 + 占位值运行期拒绝。
- **安装/下载链路**：可安装目录 Install-APKs 安装前实测校验两 APK SHA-256（均与 SHA256SUMS 一致）；Gradle distribution 固定官方 SHA-256。
- **NativeAOT**：Android unzip 改为受控前置依赖；Harmony OHOS RID→ABI 槽位映射 + llvm-readelf 架构校验 + restore/publish 分离 + --no-restore；iOS RID 白名单 + lipo 校验 + 模拟器架构按宿主选择。
- **构建健壮性**：Android 归档源清单 sidecar 检出删除；release signingConfig 不入库材料；apksigner/SDK 根解析；iOS 数据归档缺失硬失败 + sha256 sidecar；Harmony CMake 强制必需 soname；native_bridge 错误传播。

### 执行的校验命令与结果
- `[Parser]::ParseFile`（PowerShell）：Build-AndroidPackage.ps1、两个防火墙脚本、Install-APKs.ps1 均 **0 错误**。
  - 注：Build-AndroidPackage.ps1 含中文注释，已按 UTF-8 **带 BOM** 保存以消除 PS5.1 无 BOM 误按 GBK 解码导致的假报错。
- `[xml]` 解析：AndroidManifest + 主/debug network_security_config **全部通过**。
- 括号配平：5 个 build.gradle/CMakeLists 花括号/圆括号均成对。
- `Get-FileHash`：可安装两 APK 实测与 SHA256SUMS **一致**；gradle-wrapper.jar 实测 SHA-256 已记录。
- `Invoke-WebRequest .../gradle-8.8-all.zip.sha256`：取得官方值 f8b4f477…c961 并写入 wrapper properties。

### 无法完整构建的缺口（如实说明）
- 本机无 `gradle`/`groovy`/`cmake`/Android SDK/NDK/Xcode/ohpm/DevEco，**未执行** assembleDebug/assembleRelease/configure/ohos build/xcodebuild。以上 Gradle/Groovy/CMake/ArkTS/C++ 改动均为人工编写 + 括号/XML/路径静态校验，**未编译验证**。
- WSL bash 无法访问 `D:\`（localhost 限制），两个 `.sh` 未跑 `bash -n`；已人工核对语法。
- **DEP-003**：本地 wrapper jar 实测哈希与官方 Gradle 8.8 不一致（审计结论），但本机无法取得并校验官方二进制，故**未替换 jar**；仅落实了 DEP-004（distribution 校验和）。建议在可联网的干净环境执行 `gradle wrapper --gradle-version 8.8` 重新生成 jar 并把结果哈希固定到 CI。
- **H-03/I-03** 的"短期/可撤销令牌、归档来源签名"涉及服务端（OpenMU 目录，非本片），本片仅做客户端/构建侧加固。

### 遗留项
- 需在有 Android SDK/Gradle 的环境实跑 `:game-app:assembleDebug`、`:gm-app:assembleRelease`（带 keystore）复验。
- DEP-003 wrapper jar 重新生成与哈希固化。