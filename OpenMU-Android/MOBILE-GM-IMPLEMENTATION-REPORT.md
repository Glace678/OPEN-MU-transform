# Android 自动登录与原生手机 GM 实现报告

日期：2026-09-06

## 修改范围

- `game-app` 与 `gm-app` 均要求构建参数 `OPENMU_MOBILE_PACKAGE_KEY`，仅接受 43 位 base64url 字符。
- `game-app` 从包密钥以 HMAC-SHA256 派生固定游戏账号和密码，并在 SDL 原生线程启动前设置四个环境变量。身份不写入配置、日志或界面。
- 游戏设置的服务器地址只接受本机、RFC1918 或 `169.254/16` 的严格 IPv4 地址，避免免登录凭据被配置到公网或任意主机名。
- `gm-app` 已移除 WebView、Cookie 和网页登录流程，改为原生可滚动表单；保留服务器地址设置、刷新、SafeArea 和销毁时的后台任务清理。
- 所有 HTTP 请求都在单线程 `ExecutorService` 中执行，UI 更新通过主线程回调完成。切换服务器会废弃旧地址的异步结果。

## 移动 GM API

所有请求发送 `X-OpenMU-Mobile-Key`，基址后固定追加 `/api/mobile-gm`。

| 操作 | 请求 | 客户端读取或发送的字段 |
| --- | --- | --- |
| 状态 | `GET /status` | 账号优先 `accountName`，兼容 `localAccount.loginName`、`localAccount.name`、`account`、`loginName`；角色优先 `characters`，兼容 `onlineCharacters`；角色 ID 为 `characterId` 或 `id`，名称为 `name` 或 `characterName` |
| 搜索 | `GET /items?q=...` | 支持根数组或 `{items: []}`；读取 `group`、`number`、`name`（兼容 `displayName`）、`maxLevel`（兼容 `maximumLevel`）、`hasSkill`（兼容 `canHaveSkill`） |
| 发放 | `POST /grant-item` | 固定发送 `requestId`、`characterId`、`group`、`number`、`level`、`quantity`、`hasSkill`；每次点击生成新 UUID，响应期间禁用发送按钮 |

超时、连接失败、非 2xx HTTP、无效 JSON，以及发放响应中的 `success: false` 都会在原生页面显示明确错误。数量固定为 1 到 10，等级滑块为 0 到物品最大等级；所有操作控件不小于 44dp。

## 验证

使用测试密钥（43 个 `A`）执行：

```powershell
$mobileKey = 'A' * 43
$keyArgument = '-POPENMU_MOBILE_PACKAGE_KEY={0}' -f $mobileKey
.\gradlew.bat :game-app:compileDebugJavaWithJavac :gm-app:compileDebugJavaWithJavac :game-app:lintDebug :gm-app:lintDebug :gm-app:assembleDebug :gm-app:assembleRelease $keyArgument
.\Test-MobileLogic.ps1
```

结果：`BUILD SUCCESSFUL`，两个模块 Java 编译通过，GM Debug/Release APK 组装通过；lint 均为 0 error。纯逻辑测试分别为 3/3 和 3/3 通过，覆盖固定 HMAC 派生向量、非法密钥、游戏服务器 IPv4 私网限制，以及 GM HTTP 私网/公网与 HTTPS 地址策略。测试脚本绕开了当前中文工作区路径下 Gradle 8.8 test worker 的 classpath 解码问题。

42 位密钥配置测试在 Gradle 配置阶段按预期失败，错误为：`OPENMU_MOBILE_PACKAGE_KEY must be exactly 43 base64url characters`。两个生成的 `BuildConfig.java` 均确认包含 `MOBILE_PACKAGE_KEY`。

## 已知安全取舍

默认 GM 地址使用局域网 HTTP，并且现有 network security config 允许明文流量，因此移动包密钥在 HTTP 局域网内不具备传输加密。客户端只允许 HTTP 连接本机、RFC1918、IPv4 link-local、IPv6 ULA/link-local 地址，避免把密钥通过明文 HTTP 发送到公网；公网部署必须使用 HTTPS。若局域网本身不可信，也应由服务端提供 HTTPS。
