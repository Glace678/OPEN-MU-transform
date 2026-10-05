# Shard-D 修复结果（根脚本 / 根工作流 / OpenMU-数值重设计 + 供应链 #1–#33 总表协调）

工作根目录：`D:\openmu自用`　|　审计基准：`all-findings-revised-2026-10-04.md`　|　所有改动保持未提交（无 commit/push）

## 一、逐项结果（finding ID | 文件:行 | 状态 | 验证方式）

| finding ID | 文件 | 状态 | 验证方式 |
|---|---|---|---|
| PLAT-B01 | `OpenMU-数值重设计\integration\EngineProbe\EngineProbe.csproj` | 已固定 | dotnet build Release 0警告0错误；通配 *.dll 改为 MUnique.OpenMU.*.dll 前缀，不再吞入任意第三方 DLL |
| PLAT-B02 | `OpenMU-数值重设计\Verify.ps1` | 已固定 | AST 解析通过；关键源文件缺失改为 throw，不再静默跳过 |
| PLAT-B03 | `OpenMU-数值重设计\src\BalanceLab\Checks.cs` | 已固定 | dotnet build 通过；ValidateDesign 顶部加 RequireNotNull 段守卫；实跑 30248 passed/20 failed（20 项为既有 RF/vitality 经济调优缺口，非结构回归） |
| PLAT-B04 | `OpenMU-数值重设计\src\BalanceLab\Program.cs; integration\EngineProbe\Program.cs` | 已固定 | 两处输出路径均约束在 artifacts 受控根；越界请求返回 2/拒绝写入；dotnet build 0错误 |
| PLAT-R01 | `deploy_server_update.py` | 已固定 | py_compile 通过；回滚停止新进程并重启旧 launcher（保留上轮正确修复） |
| PLAT-R02 | `deploy_server_update.py` | 已固定 | py_compile 通过；manifest 逐条更新含 target.stat() 包入 try/except，失败回滚 |
| PLAT-R03 | `deploy_server_update.py` | 已固定 | py_compile 通过；回滚重启改用 os.startfile，不再拼 PowerShell 命令字符串 |
| PLAT-R04 | `provision_secrets.ps1` | 已固定 | AST 解析通过；AdminPassword 为 [SecureString]；修复上轮 string.Empty 语法错误（改为 ''） |
| PLAT-R05 | `wakeup_claude.py` | 已固定 | py_compile 通过；--dangerously-skip-permissions 替换为 --permission-mode acceptEdits（保留上轮正确修复） |
| PLAT-R06 | `deploy_server_update.py` | 已固定 | py_compile 通过；新增 new_files 集合跟踪全新目标，回滚时删除 |
| PLAT-R07 | `start_client.ps1` | 已固定 | AST 解析通过；重写为 Data\Keys\local-client.env sidecar（对齐 MuMain LocalLoginCredentials.cpp 契约），客户端退出清理，环境仅置非密变量 |
| PLAT-R08 | `build_main.bat` | 已固定 | 改用 vswhere 定位 VS，去除硬编码 D:\Microsoft Visual Studio |
| PLAT-R09 | `diag_build.bat` | 已固定 | 改用 %~dp0 + CMakeCache.txt 存在性检查，去除硬编码 VS 路径 |
| PLAT-R10 | `patch_manifest.py` | 已固定 | py_compile 通过；写 live manifest 前先校验全部目标存在 |
| PLAT-R11 | `.vscode\settings.json` | 已固定 | JSON 校验通过；cwd 改为 ${workspaceFolder}/MuMain 相对工作区 |
| PLAT-G01 | `.github\workflows\gm_probe.py` | 已固定 | py_compile 通过；github_token 仅发往 .github.ai 主机，Azure 用独立 AZURE_MODELS_TOKEN |
| PLAT-G02 | `.github\workflows\gm-probe.yml` | 已固定 | actions/checkout@v4 钉为 b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1 |
| PLAT-G03 | `.github\workflows\gm_probe.py` | 已固定 | py_compile 通过；show_models/show_chat 对不可解析 2xx 均计入 failures 并 SystemExit(1) |
| NEW-ROOT-01 | `provision_secrets.ps1` | 已固定 | AST 通过；删除 Write-Output GAME_PASS=*；调用方 local-credentials.ps1 直接读 DPAPI 文件，无人解析该 stdout |
| NEW-ROOT-02 | `wakeup_claude.py` | 已固定 | py_compile 通过；改用 subprocess.Popen(cmd_args, cwd=...) 数组形式，不再拼接转义工作区路径 |
| DEP-001 | `OpenMU/deploy/**/docker-compose.yml` | 未修复（仍可变 tag，无 digest） | OpenMU 部署 Compose 仅 tag/隐式 latest（nginx:alpine、munique/openmu-* 无 tag） | 可达性:构建链/字节漂移，无 CVE | 归属:Shard A |
| DEP-002 | `OpenMU/.github/workflows/*.yml; MuMain/.github/workflows/*.yml; 根 gm-probe.yml` | 部分闭环：根 gm-probe checkout 已钉 b4ffde…；A/B 树仍可变 tag | OpenMU claude.yml checkout@v4/claude-code-action@v1、MuMain linux-build checkout/setup-dotnet/upload-artifact@v4；根 gm-probe 已钉 SHA | 可达性:构建链/上游漂移 | 归属:Shard A/B（本片根贡献=G-02） |
| DEP-003 | `OpenMU-Android/gradle/wrapper/gradle-wrapper.jar` | 未修复（JAR 身份仍待可信重建） | 仓内 JAR SHA=2c23278a…df5e5 ≠ 官方 Gradle 8.8 JAR cb0da6…156b | 可达性:身份未确认（非证恶意） | 归属:Shard C |
| DEP-004 | `OpenMU-Android/gradle/wrapper/gradle-wrapper.properties` | 已闭环（distributionSha256Sum 已落地） | 已加 distributionSha256Sum=f8b4f477…c961（官方 gradle-8.8-all.zip） | 可达性:构建链完整性 | 归属:Shard C |
| DEP-005 | `OpenMU/src/Directory.Packages.props:45` | 未修复（仍1.10.0，待升至≥1.15.3） | OpenTelemetry.Exporter.OpenTelemetryProtocol=1.10.0 | 可达性:命中 GHSA-4625-4j76-fww9/CVE-2026-42191（修1.15.3） | 归属:Shard A |
| DEP-006 | `OpenMU/src/Directory.Packages.props:46` | 未修复（仍1.10.0，待升至≥1.15.3） | OpenTelemetry.Exporter.Zipkin=1.10.0 | 可达性:命中 GHSA-88hf-wf7h-7w4m/CVE-2026-41310（修1.15.3） | 归属:Shard A |
| DEP-007 | `OpenMU-Android build 配置` | 未修复（待对齐基线） | AGP 8.2.1 / compileSdk 36 / BuildTools 36 偏离维护基线 | 可达性:构建链可复现，无 CVE | 归属:Shard C |
| DEP-008 | `OpenMU-Android 构建脚本` | 未修复 | NDK fallback + apt 装 unzip，非确定性环境 | 可达性:构建链可复现 | 归属:Shard C |
| DEP-009 | `OpenMU (NuGet)` | 未修复（待 NuGet lock） | 无 packages.lock.json，传递闭包未锁 | 可达性:构建链可复现 | 归属:Shard A |
| DEP-010 | `OpenMU-Android (Gradle)` | 未修复 | 无 gradle.lockfile/verification-metadata | 可达性:构建链可复现 | 归属:Shard C |
| DEP-011 | `OpenMU-HarmonyOS (ohpm)` | 未修复 | ohpm 无 lock 文件 | 可达性:构建链可复现 | 归属:移动片 |
| DEP-012 | `OpenMU-HarmonyOS NativeAOT` | 未修复 | 外部原生/native 输入未钉版本/哈希 | 可达性:构建链可复现 | 归属:移动片 |
| DEP-013 | `OpenMU Dockerfile (.NET base/apk)` | 未修复 | 基础镜像/apk 包漂移（与001相邻） | 可达性:构建链可复现 | 归属:Shard A |
| DEP-014 | `各 .github/workflows Actions` | 随 DEP-002 推进 | 可变 tag（与002相邻） | 可达性:构建链可复现 | 归属:Shard A/B/本片 |
| DEP-015 | `OpenMU/.github/workflows/docs-website.yml` | 未修复 | pull_request 触发无显式 permissions 块 | 可达性:权限面 | 归属:Shard A |
| DEP-016 | `全仓` | 未修复 | 无 NOTICE/SBOM 第三方许可清单 | 可达性:许可证追溯 | 归属:跨片 |
| DEP-017 | `OpenMU 打包/Postgres 镜像` | 未修复 | 打包脚本删 license/doc 目录；.dockerignore 排除 LICENSE | 可达性:许可证材料 | 归属:Shard A |
| DEP-018 | `OpenMU/src/Directory.Packages.props:75` | 未修复（治理项） | MathParser.org-mXparser=4.4.2 维护停滞/许可待核 | 可达性:维护/许可证 | 归属:Shard A |
| DEP-019 | `OpenMU/src/Directory.Packages.props:77` | 未修复（治理项） | SixLabors.ImageSharp.Drawing=1.0.0 已停更 | 可达性:维护 | 归属:Shard A |
| DEP-020 | `MuMain ConstantsReplacer (UDE)` | 未修复（治理项） | UDE.CSharp=1.1.0 NuGet 无 license 字段 | 可达性:维护/许可证 | 归属:Shard B |
| DEP-021 | `OpenMU NuGet analyzer/测试包` | 未修复（治理项） | 传递/analyzer 包许可与传递闭包未核 | 可达性:许可证/可复现 | 归属:Shard A |
| DEP-022 | `OpenMU/docs-website/package-lock.json:7520` | 未修复（待升1.1.21重建lock） | brace-expansion=1.1.18 | 可达性:命中 GHSA-6j4f/qhr7/q2hr（修1.1.21） | 归属:Shard A |
| DEP-023 | `OpenMU/docs-website/package-lock.json:7530` | 未修复（上游暂无补丁，锁定观望） | braces=3.0.3 | 可达性:命中 GHSA-vfj7-8cjw-p6xm（暂无修复版） | 归属:Shard A |
| DEP-024 | `OpenMU/docs-website/package-lock.json:9812` | 未修复（待升3.1.8） | fast-uri=3.1.6 | 可达性:命中 GHSA-58mr/qw65/hrr3（修3.1.8） | 归属:Shard A |
| DEP-025 | `OpenMU/docs-website/package-lock.json:10781` | 未修复（上游暂无补丁，锁定观望） | http-cache-semantics=4.2.0 | 可达性:命中 GHSA-ch52-4w7c-c8xp（暂无修复版） | 归属:Shard A |
| DEP-026 | `OpenMU/docs-website/package-lock.json:10933` | 未修复（待升2.0.3） | image-size=2.0.2 | 可达性:命中 GHSA-5p2g/w3rx（修2.0.3） | 归属:Shard A |
| DEP-027 | `OpenMU/docs-website/package-lock.json:11434` | 未修复（待升17.13.7） | joi=17.13.6 | 可达性:命中 GHSA-6h2x-m376-mqjq（修17.13.7） | 归属:Shard A |
| DEP-028 | `OpenMU/docs-website/package-lock.json:11453` | 未修复（待升4.3.2） | js-yaml=4.3.1 | 可达性:命中 GHSA-2883-xcg3-v3hh（修4.3.2） | 归属:Shard A |
| DEP-029 | `OpenMU/docs-website/package-lock.json:16793` | 未修复（待升6.16.0） | qs=6.15.3 | 可达性:命中 GHSA-4mjr/x5fp（修6.16.0） | 归属:Shard A |
| DEP-030 | `OpenMU/docs-website/package-lock.json:18504` | 未修复（待升3.3.5） | svgo=3.3.4 | 可达性:命中 GHSA-4vpr/w27v（修3.3.5） | 归属:Shard A |
| DEP-031 | `OpenMU/docs-website/package-lock.json:18865` | 未修复（待升7.29.1） | undici=7.29.0 | 可达性:命中10项 GHSA（修7.29.1） | 归属:Shard A |
| DEP-032 | `MuMain/tools/Localization/package-lock.json:326` | 未修复（severity questionable，待升6.28.1） | undici=6.28.0 (node-gyp 传递) | 可达性:命中3项 GHSA（修6.28.1） | 归属:Shard B |
| DEP-033 | `OpenMU/docs-website (webpack-dev-middleware)` | 未修复/待核（不计确认命中） | webpack-dev-middleware=7.4.5 | 可达性:GHSA-g84c-rxfj-3j2c 范围与7.4.5边界矛盾（未证实命中） | 归属:Shard A |

## 二、根脚本 / 工作流 / 重设计工具修复摘要

**保留上轮正确修复**：R-01（回滚停新进程+重启旧 launcher）、R-05（`--dangerously-skip-permissions`→`--permission-mode acceptEdits`）、NEW-ROOT-02（`subprocess.Popen(cmd_args, cwd=...)` 数组传参，不再拼 PS 命令串）、R-04（`AdminPassword` 为 `[SecureString]`）、G-01（`github_token` 仅发 `.github.ai`，Azure 用独立 `AZURE_MODELS_TOKEN` + failures 聚合 `SystemExit(1)`）。

**本片新补/修正**：
- R-02：manifest 逐条更新（含 `target.stat()`）包入 try/except→失败回滚。
- R-03：回滚重启由 `f"Start-Process '{...}'"` 改为 `os.startfile(LAUNCHER_EXE)`。
- R-06：新增 `new_files` 集合跟踪全新生成目标，回滚时删除。
- R-07：**纠正上轮错误修复**。上轮写 GUID 命名 `game-login-<guid>.tmp` 并置 `MU_LOCAL_LOGIN_FILE`，但核对 `MuMain/src/source/Network/Login/LocalLoginCredentials.cpp` 确认客户端只读 `MU_LOCAL_GAME_USERNAME/PASSWORD` 与 `Data\Keys\local-client.env` sidecar（NAME=VALUE），从不读 `MU_LOCAL_LOGIN_FILE`。已重写 start_client.ps1：写 `Data\Keys\local-client.env`（`MU_LOCAL_AUTO_LOGIN=1`+用户名+密码），ACL 限当前用户，子进程环境仅置非密变量，客户端退出经 `Register-ObjectEvent`+`finally` 删除 sidecar。
- R-08/R-09：`build_main.bat`/`diag_build.bat` 经 `vswhere` 定位 VS，去除硬编码 `D:\Microsoft Visual Studio`；diag_build 用 `%~dp0`+CMakeCache.txt 存在性检查。
- R-10：`patch_manifest.py` 写 live manifest 前先校验全部目标存在。
- R-11：`.vscode/settings.json` cwd 改 `${workspaceFolder}/MuMain`。
- NEW-ROOT-01：删除 provision_secrets.ps1 的 `Write-Output GAME_PASS=$gamePass`；确认 local-credentials.ps1 直接读 DPAPI 文件、无人解析该 stdout。
- G-02：根 `gm-probe.yml` `actions/checkout` 钉 SHA `b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1`。
- G-03：`gm_probe.py` show_models/show_chat 对不可解析 2xx 均计入 failures。
- B-01：EngineProbe.csproj 通配 `*.dll` 收敛为 `MUnique.OpenMU.*.dll` 前缀 allowlist。
- B-02：Verify.ps1 关键源文件缺失改为 throw。
- B-03：BalanceLab `Checks.ValidateDesign` 顶部加 `RequireNotNull` 段守卫（combat/growth/profiles/gear/classes/skills/monsters/loot/economy/enhancement/progression）。
- B-04：BalanceLab/Program.cs 与 EngineProbe/Program.cs 输出路径约束在 `artifacts` 受控根，越界拒绝。
- **修复一处上轮引入的语法错误**：provision_secrets.ps1:12 的 C# 风格 `string.Empty` 在 PowerShell 下 AST 解析报错，改为 `''`。

## 三、权威供应链状态表 DEP-001…DEP-033

| ID | 当前锁定版本/落点 | 公告匹配 | 本仓可达性 | 修复落点 | 归属片 | 状态 |
|---|---|---|---|---|---|---|
| DEP-001 | OpenMU 部署 Compose 仅 tag/隐式 latest（nginx:alpine、munique/openmu-* 无 tag） | 构建链/字节漂移，无 CVE | 重新 pull/缓存缺失时取得不同字节 | OpenMU/deploy/**/docker-compose.yml | Shard A | 未修复（仍可变 tag，无 digest） |
| DEP-002 | OpenMU claude.yml checkout@v4/claude-code-action@v1、MuMain linux-build checkout/setup-dotnet/upload-artifact@v4；根 gm-probe 已钉 SHA | 构建链/上游漂移 | 上游 tag 移动即影响 CI | OpenMU/.github/workflows/*.yml; MuMain/.github/workflows/*.yml; 根 gm-probe.yml | Shard A/B（本片根贡献=G-02） | 部分闭环：根 gm-probe checkout 已钉 b4ffde…；A/B 树仍可变 tag |
| DEP-003 | 仓内 JAR SHA=2c23278a…df5e5 ≠ 官方 Gradle 8.8 JAR cb0da6…156b | 身份未确认（非证恶意） | 执行 wrapper 即加载该 JAR | OpenMU-Android/gradle/wrapper/gradle-wrapper.jar | Shard C | 未修复（JAR 身份仍待可信重建） |
| DEP-004 | 已加 distributionSha256Sum=f8b4f477…c961（官方 gradle-8.8-all.zip） | 构建链完整性 | 缓存缺失下载时绑定字节 | OpenMU-Android/gradle/wrapper/gradle-wrapper.properties | Shard C | 已闭环（distributionSha256Sum 已落地） |
| DEP-005 | OpenTelemetry.Exporter.OpenTelemetryProtocol=1.10.0 | 命中 GHSA-4625-4j76-fww9/CVE-2026-42191（修1.15.3） | 需磁盘重试+未指定专用目录，未证实 | OpenMU/src/Directory.Packages.props:45 | Shard A | 未修复（仍1.10.0，待升至≥1.15.3） |
| DEP-006 | OpenTelemetry.Exporter.Zipkin=1.10.0 | 命中 GHSA-88hf-wf7h-7w4m/CVE-2026-41310（修1.15.3） | 需实际 Zipkin+高基数 span，未证实 | OpenMU/src/Directory.Packages.props:46 | Shard A | 未修复（仍1.10.0，待升至≥1.15.3） |
| DEP-007 | AGP 8.2.1 / compileSdk 36 / BuildTools 36 偏离维护基线 | 构建链可复现，无 CVE | Android 生产构建输入 | OpenMU-Android build 配置 | Shard C | 未修复（待对齐基线） |
| DEP-008 | NDK fallback + apt 装 unzip，非确定性环境 | 构建链可复现 | 构建机环境漂移 | OpenMU-Android 构建脚本 | Shard C | 未修复 |
| DEP-009 | 无 packages.lock.json，传递闭包未锁 | 构建链可复现 | restore 字节不可复现 | OpenMU (NuGet) | Shard A | 未修复（待 NuGet lock） |
| DEP-010 | 无 gradle.lockfile/verification-metadata | 构建链可复现 | 依赖传递闭包未绑字节 | OpenMU-Android (Gradle) | Shard C | 未修复 |
| DEP-011 | ohpm 无 lock 文件 | 构建链可复现 | Harmony 依赖未锁 | OpenMU-HarmonyOS (ohpm) | 移动片 | 未修复 |
| DEP-012 | 外部原生/native 输入未钉版本/哈希 | 构建链可复现 | 外部注入库 | OpenMU-HarmonyOS NativeAOT | 移动片 | 未修复 |
| DEP-013 | 基础镜像/apk 包漂移（与001相邻） | 构建链可复现 | 镜像重建字节漂移 | OpenMU Dockerfile (.NET base/apk) | Shard A | 未修复 |
| DEP-014 | 可变 tag（与002相邻） | 构建链可复现 | 上游 tag 移动 | 各 .github/workflows Actions | Shard A/B/本片 | 随 DEP-002 推进 |
| DEP-015 | pull_request 触发无显式 permissions 块 | 权限面 | PR 可影响 docs 构建 | OpenMU/.github/workflows/docs-website.yml | Shard A | 未修复 |
| DEP-016 | 无 NOTICE/SBOM 第三方许可清单 | 许可证追溯 | 合规 | 全仓 | 跨片 | 未修复 |
| DEP-017 | 打包脚本删 license/doc 目录；.dockerignore 排除 LICENSE | 许可证材料 | 最终镜像缺许可文本 | OpenMU 打包/Postgres 镜像 | Shard A | 未修复 |
| DEP-018 | MathParser.org-mXparser=4.4.2 维护停滞/许可待核 | 维护/许可证 | NuGet 生产依赖 | OpenMU/src/Directory.Packages.props:75 | Shard A | 未修复（治理项） |
| DEP-019 | SixLabors.ImageSharp.Drawing=1.0.0 已停更 | 维护 | NuGet 生产依赖 | OpenMU/src/Directory.Packages.props:77 | Shard A | 未修复（治理项） |
| DEP-020 | UDE.CSharp=1.1.0 NuGet 无 license 字段 | 维护/许可证 | 独立工具，非游戏运行时 | MuMain ConstantsReplacer (UDE) | Shard B | 未修复（治理项） |
| DEP-021 | 传递/analyzer 包许可与传递闭包未核 | 许可证/可复现 | 构建工具链 | OpenMU NuGet analyzer/测试包 | Shard A | 未修复（治理项） |
| DEP-022 | brace-expansion=1.1.18 | 命中 GHSA-6j4f/qhr7/q2hr（修1.1.21） | lock-only docs 构建，不可信 pattern 未证实 | OpenMU/docs-website/package-lock.json:7520 | Shard A | 未修复（待升1.1.21重建lock） |
| DEP-023 | braces=3.0.3 | 命中 GHSA-vfj7-8cjw-p6xm（暂无修复版） | lock-only docs，调用未证实 | OpenMU/docs-website/package-lock.json:7530 | Shard A | 未修复（上游暂无补丁，锁定观望） |
| DEP-024 | fast-uri=3.1.6 | 命中 GHSA-58mr/qw65/hrr3（修3.1.8） | 仓内无 URL 安全决策消费者 | OpenMU/docs-website/package-lock.json:9812 | Shard A | 未修复（待升3.1.8） |
| DEP-025 | http-cache-semantics=4.2.0 | 命中 GHSA-ch52-4w7c-c8xp（暂无修复版） | docs 静态构建无共享缓存 | OpenMU/docs-website/package-lock.json:10781 | Shard A | 未修复（上游暂无补丁，锁定观望） |
| DEP-026 | image-size=2.0.2 | 命中 GHSA-5p2g/w3rx（修2.0.3） | 构建时处理已提交图片挂起 | OpenMU/docs-website/package-lock.json:10933 | Shard A | 未修复（待升2.0.3） |
| DEP-027 | joi=17.13.6 | 命中 GHSA-6h2x-m376-mqjq（修17.13.7） | 仓内无 isoDate 攻击输入 | OpenMU/docs-website/package-lock.json:11434 | Shard A | 未修复（待升17.13.7） |
| DEP-028 | js-yaml=4.3.1 | 命中 GHSA-2883-xcg3-v3hh（修4.3.2） | 恶意 front-matter 路径未证实 | OpenMU/docs-website/package-lock.json:11453 | Shard A | 未修复（待升4.3.2） |
| DEP-029 | qs=6.15.3 | 命中 GHSA-4mjr/x5fp（修6.16.0） | 特定 parse 选项+不可信 query 未证实 | OpenMU/docs-website/package-lock.json:16793 | Shard A | 未修复（待升6.16.0） |
| DEP-030 | svgo=3.3.4 | 命中 GHSA-4vpr/w27v（修3.3.5） | 仓内无可信 SVG 上传/XSS 链 | OpenMU/docs-website/package-lock.json:18504 | Shard A | 未修复（待升3.3.5） |
| DEP-031 | undici=7.29.0 | 命中10项 GHSA（修7.29.1） | 静态/local-search 构建，WebSocket/缓存路径未证实 | OpenMU/docs-website/package-lock.json:18865 | Shard A | 未修复（待升7.29.1） |
| DEP-032 | undici=6.28.0 (node-gyp 传递) | 命中3项 GHSA（修6.28.1） | 工具链，仓内无 npm CI/run 入口 | MuMain/tools/Localization/package-lock.json:326 | Shard B | 未修复（severity questionable，待升6.28.1） |
| DEP-033 | webpack-dev-middleware=7.4.5 | GHSA-g84c-rxfj-3j2c 范围与7.4.5边界矛盾（未证实命中） | 待核 | OpenMU/docs-website (webpack-dev-middleware) | Shard A | 未修复/待核（不计确认命中） |

> 说明：本片（Shard D）仅可编辑根目录与 `OpenMU-数值重设计`；落在 `OpenMU/`、`MuMain/`、`OpenMU-Android/HarmonyOS/iOS`、`OpenMU-安卓手机版-可安装` 的供应链项均为**只读核对**，代码升级由对应片落地。所有 npm/.NET 版本均以 `package-lock.json`/`Directory.Packages.props` 实测读取为准。

## 四、验证命令与结果

```powershell
# Python 编译检查（根脚本 + 根工作流脚本）
python -m py_compile deploy_server_update.py patch_manifest.py wakeup_claude.py .github\workflows\gm_probe.py
# => exit 0  PY_COMPILE_ALL: OK

# PowerShell AST 解析（3 个改过的 .ps1）
$errs=$null;[System.Management.Automation.Language.Parser]::ParseFile($f,[ref]$tokens,[ref]$errs)
# => provision_secrets.ps1 OK / start_client.ps1 OK / Verify.ps1 OK
#    （首轮 provision_secrets.ps1:12 报 '参数列表中缺少参量'，改 string.Empty->'' 后复解析 OK）

# .NET 构建（B-01/B-03/B-04 编译与程序集解析）
dotnet build src\BalanceLab -c Release      => 已成功 0警告 0错误
dotnet build integration\EngineProbe -c Release => 已成功 0警告 0错误

# BalanceLab 实跑（B-03 守卫不回归 happy-path）
dotnet run --project src\BalanceLab --no-build -c Release -- verify design\balance.v1.json artifacts
# => Checks: 30248 passed, 20 failed；20 项为既有 RF/vitality 80+ 经济调优缺口（Positive/Net Zen），非结构回归；无 'Missing required design section'

# .vscode/settings.json JSON 校验 => valid JSON
# 供应链只读核对：
#   Directory.Packages.props:45,46 => OTLP/Zipkin 仍 1.10.0
#   docs-website package-lock => brace-expansion1.1.18 / braces3.0.3 / fast-uri3.1.6 /
#     http-cache-semantics4.2.0 / image-size2.0.2 / joi17.13.6 / js-yaml4.3.1 / qs6.15.3 /
#     svgo3.3.4 / undici7.29.0 / webpack-dev-middleware7.4.5
#   MuMain Localization package-lock => undici6.28.0 (node-gyp12.4.0)
#   Android gradle-wrapper.properties => distributionSha256Sum 已落地（f8b4f477…）
#   Android wrapper JAR SHA => 2c23278a… 仍 ≠ 官方 8.8 cb0da6…
#   OpenMU deploy compose => 仍可变 tag/无 digest；claude.yml/linux-build.yml Actions 仍可变 tag
```

## 五、遗留缺口

1. **根脚本/工作流/重设计工具（本片范围）**：无遗留；20 个本片 finding 全部固定并通过语法/构建验证。
2. **供应链闭环 1 项**：仅 DEP-004（Android dist SHA-256）已闭环。
3. **供应链部分闭环 1 项**：DEP-002——本片根 `gm-probe.yml` checkout 已钉 SHA；OpenMU（claude.yml 等）与 MuMain（linux-build.yml）workflow 仍用可变 tag，待 Shard A/B 钉 SHA。
4. **供应链遗留 31 项**（归对应片，本片只读记录）：
   - Shard A：DEP-001（compose digest）、DEP-005/006（OTLP/Zipkin 升至 1.15.3）、DEP-013/015/016/017/018/019/021（Docker drift、docs 权限、SBOM、许可、mXparser/ImageSharp.Drawing/NuGet 治理）、DEP-022–031 与 033（docs-website npm lock 升级重建）。
   - Shard B：DEP-020（UDE 治理）、DEP-032（Localization undici 6.28.1）。
   - Shard C：DEP-003（wrapper JAR 身份重建）、DEP-007/008/010（AGP 基线、NDK/apt、Gradle lockfile）。
   - 移动片：DEP-011/012（ohpm lock、NativeAOT 外部输入）。
5. **无法离线验证/无上游补丁**：DEP-023（braces 3.0.3）、DEP-025（http-cache-semantics 4.2.0）截至复核无修复版，按原则只锁定记录、不破坏构建；DEP-033 webpack-dev-middleware 7.4.5 公告范围自相矛盾，不计确认命中，待核。
6. BalanceLab 实跑 20 项 failed 为既有经济调优内容缺口（Positive/Net Zen at RF/vitality 80+），与本片结构加固无关，作为设计调优遗留记录，不计作回归。
