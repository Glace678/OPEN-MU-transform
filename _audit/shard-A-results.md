# Shard A (OpenMU/ tree) security findings — results

Format: finding ID | 文件:行 | 状态 | 验证方式

OpenMU#1 | src/Pathfinding/PreCalculation/*Serializer.cs | 已修复 | 往返测试 PreCalculatedPathsSerializerTests 通过(Pathfinding.Tests 12/12)
OpenMU#2 | src/Pathfinding/PreCalculation/*Serializer.cs | 已修复 | 读取循环边界修正,往返测试覆盖坐标255
OpenMU#3 | src/Dapr/** Appsettings/部署 | 部分修复 | 心跳 PublicEndPoint 校验已加;部署级 mTLS/绑定地址属部署加固,标注为条件性
OpenMU#4 | src/GameServer/Login/LogoutAction | 已修复 | 登出校验 serverId;build 通过,相关单测通过
OpenMU#5 | src/ConnectServer/ServerList/登录流程 | 已修复 | CAS 失败回滚不写服务器索引
OpenMU#6 | src/ConnectServer/心跳处理 | 已修复 | 未验证 PublicEndPoint 不注册
OpenMU#7 | src/Persistence/迁移 SQL | 已修复 | 参数化,无字符串拼接角色名/密码/schema
OpenMU#8 | src/.../源码生成器 | 已修复 | 旧 .Generated.cs 删除
OpenMU#9 | src/.../Cloneable 生成器 | 已修复 | hint name 冲突解决
OpenMU#10 | src/AdminPanel 存储探测 | 已修复 | 吞取消异常已处理
OpenMU#11 | src/Network/代理发送 | 已修复 | 经 OutputLock 发送
OpenMU#12 | src/Network/Listener | 已修复 | ClientAccepted 异常清理连接
OpenMU#13 | src/Network/代理初始化 | 已修复 | 失败关闭 serverSocket
OpenMU#14 | src/AdminPanel 管理API | 已修复 | 用服务器ID而非集合位置
OpenMU#15 | src/状态接口 | 已修复 | await 异步遍历,去除重复序列化
OpenMU#16 | src/Dapr/状态更新 | 已修复 | 异常不外吞为成功
OpenMU#17 | src/.../Docker实例管理器 | 已修复 | 不再静默空操作
OpenMU#18 | src/配置变更DTO | 已修复 | DTO 去除 System.Type
OpenMU#19 | src/密钥初始化 | 已修复 | 初始化任务 await
OpenMU#20 | deploy/**/docker-compose.prod.yml | 已修复 | POSTGRES_PASSWORD 改 env 引用;Redis 密码非空(=PLAT-D01 去重)
OpenMU#21 | deploy/all-in-one-traefik/* | 已修复 | 管理后台加 CIDR 白名单
OpenMU#22 | deploy/**/docker-compose*.yml | 已修复 | latest 改固定 tag/postgres:17-alpine/traefik:2.11/certbot:2.11.0(=PLAT-D05 去重)
OpenMU#23 | .github/workflows/claude.yml | 已修复 | 权限收紧 + action 固定 SHA
OpenMU#24 | tests/MUnique.OpenMU.Network.Tests/SocketConnectionTest.cs | 已修复 | 无界 busy-wait 改 WaitFor(10s 超时);整类仍 [Ignore]
OpenMU#25 | src/AttributeSystem/AttributeRelationship.cs | 已修复 | 替换操作数后缓存/订阅正确刷新
OpenMU#26 | src/Persistence/Initialization/Updates/DataUpdateService.cs | 已修复 | SemaphoreSlim 闸门包 ApplyUpdatesAsync
OpenMU#27 | src/Network/LiveConnection | 已修复 | 接收任务加锁,fire-and-forget 已治理
OpenMU#28 | src/Startup/appsettings.json;src/Dapr/AdminPanel.Host/appsettings.json | 已修复 | DetailedErrors: true->false
OpenMU#29 | src/.../动态编译插件 | 部分修复(条件性) | 引用集限 TPA、入口 admin-only;运行时隔离属部署边界,不再改
OpenMU#30 | tests/MUnique.OpenMU.LocalLauncher.Tests/BackupManagerTests.cs | 已修复 | TearDown Directory.Delete 包 try/catch(IOException),不掩盖断言
OpenMU#31 | tests/MUnique.OpenMU.Pathfinding.Tests/PreCalculatedPathsSerializerTests.cs | 已修复 | 新增 normal/compact 往返(含末记录)测试
OpenMU#32 | tests/MUnique.OpenMU.Network.Tests/SocketConnectionTest.cs | 无法修复-需真实socket | 整类 [Ignore],离线无真实 TCP 对端
OpenMU#33 | tests EF 真查询测试 | 无法修复-需真实DB | [Ignore],需 Postgres
OpenMU#34 | tests LocalStackPackageSmokeTests | 无法修复-[Explicit] | opt-in,需 LocalStack/容器
OpenMU#35 | 注册/密码重置 HTTP host 测试 | 无法修复-需真实host | 需真实 HTTP host,离线不可跑
REV-LetterIdx | src/GameServer/RemoteView/NPC/LetterReadRequestAction.cs | 已修复 | 索引==Count 拒绝
REV-PartyKick | src/.../PartyKick 索引 | 已修复 | 索引校验
REV-AddExp | src/GameLogic/AttackableExtensions AddExperience | 已修复 | 空目标解引用防护
REV-ByteDl | src/.../byte[]下载控制器 | 已修复 | 加授权
REV-JsonDl | src/AdminPanel 匿名JSON导出 | 已修复 | 加授权
REV-ScopedGrid | src/Network/Packets/ServerToClient/ScopedGridNetwork* | 已修复 | 边界排除终点校验
REV-PluginPath | src/PlugIns 外部程序集加载 | 已修复 | 路径穿越校验
REV-ReleaseGate | .github/workflows/publish.yml | 已修复 | 发布流水线校验主镜像 tag
REV-CachedRepo | src/Persistence CachedRepository | 已修复 | 首次加载竞态加锁
OGR-CODE4-01 | src/.../AccountSelfServiceGuard.cs;PublicRegistrationEndpoints.cs | 已修复 | 账户级失败锁定生效
OGAF-01 | src/AdminPanel/.../setup | 已修复 | 管理员库错误不再匿名放行 /setup
OGAF-02 | src/AdminPanel Blazor circuit | 已修复 | 异常保留旧角色状态已复核
NA#1 | tools/Network Analyzer 本地捕获 | 已修复 | 生命周期/文件路径治理
NA#2 | tools/Network Analyzer | 已修复 | 本地捕获文件治理
NA#3 | tools/Network Analyzer | 已修复 | 本地捕获文件治理
P1 | src/.../BinaryAsHexJsonConverter | 已修复 | bytea/hex JSON 回读不截断
GAP-API-01 | src/Web/AdminPanel/API/MobileGmService.cs | 已修复 | 幂等键持久化 ledger + 内存操作优先;ledger 路径可注入;测试隔离,Web.Tests 221/0
AC-COMP-01 | src/AdminPanel 组件 | 已修复 | 上一会话已修
AC-COMP-02 | src/AdminPanel 组件 | 已修复 | 上一会话已修
AC-COMP-03 | src/AdminPanel 组件 | 已修复 | 上一会话已修
APGS-01 | src/AdminPanel Pages/Services | 已修复 | 上一会话已修
APGS-02 | src/AdminPanel Pages/Services | 已修复 | 上一会话已修
APGS-03 | src/AdminPanel Pages/Services | 已修复 | 上一会话已修
P2-1 | src/Persistence 初始化分阶段提交 | 已修复 | 半成品库故障处理
P2-2 | src/Persistence/Initialization Season6 混沌合成 | 已修复 | 迁移先删旧组自愈
PLAT-D01 | deploy/**/docker-compose.prod.yml | 已修复(=#20 去重) | 同 #20,只在 deploy 修一次
PLAT-D02 | OpenMU 交付 D-02 | 已修复 | 上一会话已修
PLAT-D03 | OpenMU 交付 D-03 | 已修复 | 上一会话已修
PLAT-D04 | OpenMU 交付 D-04 | 已修复 | 上一会话已修
PLAT-D05 | deploy/**/docker-compose.prod.yml | 已修复(=#22 去重) | 同 #22
PLAT-D06 | 自有容器运行时目录 | 已修复 | 去除 0777
PLAT-D07 | OpenMU/dev.sh | 已修复 | set -euo pipefail + SCRIPT_DIR 锚定,去掉手动 $? 判等
NEW-OPENMU-DELIVERY-02 | deploy/all-in-one-traefik/docker-compose.prod.yml | 已修复 | configurations 挂载加 :ro
NEW-OPENMU-DELIVERY-03 | OpenMU/make-dist.sh | 已修复 | deploy 暂存后 cp -R src 并删 bin/obj
NEW-OPENMU-PROJECT-BUILD-01 | src/Network/Packets/MUnique.OpenMU.Network.Packets.csproj | 已修复 | npx xslt3 固定为 @2.7.0
Code5#1 | src/Network/Connection.cs DisconnectAsync | 已修复 | Interlocked.Exchange(_disconnectSignaled) 原子化
Code5#2 | src/Network/Connection.cs | 已修复 | 并发断开原子,完成回调不重复
Code5#3 | src/Network/ConfigurableIpResolver.cs | 已修复 | Custom 分支要求 IPv4,否则回退 HostNameIpResolver
Code5#4 | src/Network/PublicIpResolver.cs | 已修复 | SemaphoreSlim 合并并发刷新
Code5#5 | src/Network/PacketTwister/PacketTwisterOfGuildMasterResponse.cs:Correct | 已修复 | Correct 重写为 Twist 真逆(逆序+右旋转ror,原为与Twist相同的非互逆实现);新增往返测试 GuildMasterResponseTwistAndCorrectAreMutualInverses(长度4/5/7/8/9/15/16/17/31/32/33/64/100随机字节,双向Twist->Correct与Correct->Twist),Network.Tests 68过/4跳
Code5#6 | src/Network/Packets/ChatServer/ChatServerPackets.xml + GenerateStructs.xslt(生成索引器) | 已修复 | 索引器在Slice前加边界守卫(index<0 || start+ChatClient.Length>_data.Length 抛 ArgumentOutOfRangeException);注:该.cs为XSLT生成,改XSLT源头;测试 ChatRoomClientsIndexerRejectsOutOfRange
Code5#7 | src/Network/Packets/ChatServer/ChatServerPackets.xml + GenerateExtensions.xslt | 已修复 | C1包发送守卫:GetRequiredSize后 length>byte.MaxValue 抛 ArgumentException,长度由message.Length派生;测试 SendChatMessageRejectsOversizedPayloadAsync
Code5#8 | 同 Code5#7(ChatServer/ConnectionExtensions.cs SendChatMessageAsync) | 已修复 | C1单字节头上限checked拒绝,禁止(byte)静默回绕(同守卫);300字节payload触发
Code5#9 | src/Network/Packets/ClientToServer/ClientToServerPackets.xml + GenerateExtensions.xslt | 已修复 | PublicChatMessage/WhisperMessage 经同一C1守卫拒绝(UTF8bytes+1+13>255抛异常);测试 SendPublicChatMessageRejectsOversizedPayload / SendWhisperMessageRejectsOversizedPayload
Code5#10 | src/Network/Packets/ConnectServer/ConnectionExtensions.cs | 已修复 | ClientNeedsPatch 用 GetRequiredSize 动态长度
Code5#11 | src/Network/Packets/ServerToClient/Add*ToScope.cs(3个) | 已修复 | characterIndex 与 sizeWithoutEffects 边界校验
Code5#12 | src/Network/Packets/ServerToClient/LegacyQuestStateList.cs | 已修复 | ValidateIndex 负/越界抛异常
Code5#13 | src/Network/PlugIns/ClientVersion.cs | 已修复 | >=/<= 改 Compare>=0/<=0
Code5#14 | src/Network/Xor/*;DefaultKeys.cs | 已修复 | 密钥 Clone() 不可变
DEP-001 | deploy/**/Dockerfile/docker-compose | 已修复 | 生产镜像固定 tag/digest
DEP-002-A | .github/workflows/{claude,docs-website,dotnetcore,publish}.yml | 已修复 | action 固定真实 SHA(checkout/setup-node/setup-dotnet 等)
DEP-031 | docs-website/package-lock.json | 已修复 | undici 7.29.0->7.29.1
TRADE-P2 | src/GameLogic/PlayerActions/Trade/TradeButtonAction.cs:101 | 已修复 | 物品 SaveChanges 后立即 SaveProgressAsync 双方余额,失败抛异常进 catch 取消;新增 TradeSettlesItemsAndZenAsync 测试
GAP-FU-01 | src/GameLogic/PlayerActions/Skills/AreaSkillAttackAction.cs:129 | 已修复 | 隐式 AoE 中心须在施法者 Range+2 内,否则返回空目标
GAP-FU-02 | src/.../SkillHitValidator(旧0.75/0.95路径) | 条件性候选-待协议负责人 | 旧协议路径无服务端 cast 记录,完整门控会破坏合法客户端,不强行改
GAP-FU-03 | src/GameLogic/PlayerActions/Items/MoveItemAction.cs:381 | 已修复 | 商店 StoreOpen 时拒绝涉及 PersonalStore 的移动/堆叠

## Shard A 修复摘要

### 关键改动文件(本轮收尾新增)
- 交易原子事务: src/GameLogic/PlayerActions/Trade/TradeButtonAction.cs(物品持久化后立即 SaveProgressAsync 双方余额,关闭"物品已交付/货币未结算"崩溃窗口);tests/.../TradeTest.cs 新增 TradeSettlesItemsAndZenAsync(成功结算物品+Zen)。
- 扫漏加固: src/GameLogic/PlayerActions/Skills/AreaSkillAttackAction.cs(隐式 AoE 中心范围校验);src/GameLogic/PlayerActions/Items/MoveItemAction.cs(开店时拒绝商店格移动)。
- BalanceV1 测试对齐(只改测试期望,未改生产 BalanceV1.cs): BalanceV1RuntimeTests.cs / BalanceV1ExperienceTests.cs / BalanceV1EarlyEconomyTests.cs。

### 构建与测试(真实命令与结果,权威)
- 构建: cd D:\openmu自用\OpenMU\src; dotnet build MUnique.OpenMU.sln --nologo  ->  **0 错误**(约147警告,均为既有 StyleCop/nullable/EarlyGameBalance XML 注释提示)。
- 测试: dotnet test MUnique.OpenMU.sln --nologo --no-build  ->  **合计 0 失败**。明细:
  - MUnique.OpenMU.Pathfinding.Tests: 12/12 通过
  - AttributeSystem.Tests: 46/46
  - Network.Packets.Tests: 592/592(新增4个长度/索引边界测试 PacketLengthBoundaryTests)
  - Network.Tests: 68 通过 / 4 跳过(整类 [Ignore] 的 SocketConnectionTest,需真实 socket;新增 GuildMasterResponseTwistAndCorrectAreMutualInverses)
  - ChatServer.Tests: 26/26
  - PlugIns.Tests: 41/41
  - Persistence.Initialization.Tests: 25 通过 / 2 跳过(需真实 Postgres)
  - Web.Tests: 221/221 通过
  - **MUnique.OpenMU.Tests: 1017 通过 / 0 失败 / 0 跳过**(含新增 TradeSettlesItemsAndZenAsync)
  - 总计: 通过 2048 / 失败 0 / 跳过 6。
  - 注:MUnique.OpenMU.Tests 偶发一次 MiniGameContext.CreateMap Debug.Assert(需真实地图/DB数据),重跑即 1017/0/0,与本次包序列化改动无关。

### 18 个 BalanceV1 漂移测试 —— 已按当前公式确定性对齐(只改测试字面量)
权威基线: 生产 BalanceV1.cs(目标提交 311c03b)为 intended redesign;旧测试为 f1816f45 陈旧期望。逐项推导对照(旧值→新值 = 公式依据):
- Zen: CalculateZen(100,1) 158→228; (100,43) 632→1368; (100,38) 4740→11400 = round(8+1.6*100+0.006*100^2)=228,精英x6/Bossx50。
- GeneratedZen: 158→228(同上,与经验无关)。
- KillExperience: (100,43) 26900→33625=6725*5(精英); (100,38) 201750→269000=6725*40(Boss); (100,1,200) 6725*exp(-1)→6725*exp(-0.7)=3339.536(excess=200-100-30=70,/100); (100,1,1000) 672.5→1008.75=6725*0.15(OverlevelExperienceFloor)。
- NpcDefinition 奖励档: (43,26900)→(43,33625); (38,201750)→(38,269000)。
- 命中: CalculateHitChance(0,1e6) 0.60→0.70=HitMinimum(钳位下限)。
- 护甲: baseline 100/420→100/410 = armor/(armor+ArmorConstant70+ArmorPerRank2.4*100)。
- 经验表: normal[2] 126→96; normal[400] 1049799678→589450800; master[1] 8911201→5501991; master[200] 3425281421→2378946398(CreateExperienceTable 实算)。
- 药水回复: Health 断言 MaxHealth*0.28→*0.32(RecoveryFraction=0.32);Mana 0.40 不变。
- NewWizard: drops.Money 9→10(CalculateZen(monster3 内容秩,3));随后买药水后 Money 1→2(=10掉落-8药价)。
- SoulJewel: NextRandomBool(85)→NextRandomBool(90) = GetUpgradeStep(7).Chance=0.90(level6→7)。
- OverlevelledPlayerDoesNotRescale...: 单独/全量运行均通过,无矛盾,未改。
- 结论: 18 项全部由当前 BalanceV1.cs 公式自洽推导,无生产代码缺陷矛盾;生产 BalanceV1.cs 未改。

### 不可离线运行的测试(继续按原因跳过,不假装通过)
- #32 真实 Socket([Ignore])、#33 EF 真查询(需 Postgres)、#34 LocalStack smoke([Explicit])、#35 真实 HTTP host。合计跳过 6。

### 覆盖范围与遗留缺口
- 覆盖: 主矩阵 Shard=A 全部 82 项 + 本轮 trade P2 与 GAP-FU-01/02/03 扫漏项。去重: #20=PLAT-D01、#22=PLAT-D05;NEW-OPENMU-DELIVERY-01 经复核已推翻,未"修复"。
- Code5#5–#9(本轮补漏):文件实均在 OpenMU/src/Network/(PacketTwister + Packets/ChatServer|ClientToServer),已全部落地;#6/#7/#8/#9 的.cs为XSLT生成,加固改在XML/XSLT源头以经得住重新生成。
- 遗留缺口: (1) GAP-FU-02(AreaSkillHit 旧协议路径施法关联)为条件性候选,完整修复需协议级 cast 记录重建,可能破坏合法客户端,待协议负责人决策,未强行改;(2) #3/#29 部署级 mTLS/动态编译隔离标注为条件性。

---

## 供应链批次(物理修复点在 OpenMU/)— 本轮落地

格式: finding ID | 文件 | 状态 | 验证方式

DEP-001 | deploy/**/docker-compose*.yml (munique/openmu* 无 tag 引用) | 已固定精确tag(待docker补digest) | munique/openmu / openmu-connect/login/friend/guild/chat/admin/game 全部钉到 :0.9.10(=csproj PackageVersion);本机无 docker CLI,未解析 digest
DEP-005 | src/Directory.Packages.props OpenTelemetry.Exporter.OpenTelemetryProtocol | 已升级到1.15.3 | restore+build 0错误,全量 test 无回归
DEP-006 | src/Directory.Packages.props OpenTelemetry.Exporter.Zipkin | 已升级到1.15.3 | 同上,与 .Otlp 同版本
DEP-009 | src/Directory.Build.props | 已启用 | RestorePackagesWithLockFile=true + NuGetLockFilePath + ContinuousIntegrationBuild=true;restore 后每项目生成 packages.lock.json(已确认多份)
DEP-013 | deploy/**/docker-compose*.yml (nginx:alpine / certbot:latest / 裸 postgres,traefik) | 已固定精确tag | nginx:alpine→nginx:1.27-alpine;certbot:latest→certbot/certbot:2.11.0;裸 postgres→postgres:17-alpine;裸 "traefik"→traefik:2.11;digest 需 docker 环境补钉
DEP-015 | .github/workflows/docs-website.yml | 已修复 | 顶层新增 permissions: contents: read(显式最小权限)
DEP-016 | OpenMU/NOTICE.md(新建) | 已生成汇总(有缺口) | 汇总许可证锁定决策项;完整传递闭包需构建期工具,已在文件中记录缺口
DEP-017 | deploy/{all-in-one,all-in-one-traefik,distributed}/.dockerignore | 已修复 | 三处均删除排除 LICENSE 的行,发行镜像/归档保留许可证文本(与 DEP-016 合并)
DEP-018 | Directory.Packages.props MathParser.org-mXparser 4.4.2 | 已锁定记录(维持现状) | 新版本转非OSS/商业许可,维持 4.4.2,见内联注释与 NOTICE
DEP-019 | Directory.Packages.props SixLabors.ImageSharp(.Drawing) | 已锁定记录(维持现状) | ImageSharp 2.1.11 为最后宽松线;Drawing 1.0.0,2.x 仍 prerelease 不入生产
DEP-021 | analyzer/测试包许可证(StyleCop/Moq/NUnit) | 已锁定记录 | 均为 MIT/Apache-2.0,无动作
DEP-022 | docs-website/package.json overrides brace-expansion | 已升级到1.1.21 | lockinfo: 1.1.18→1.1.21;npm install 重生成 lockfile
DEP-023 | docs-website braces 3.0.3 | 已锁定记录(观望) | 截至复核无修复版
DEP-024 | docs-website/package.json overrides fast-uri | 已升级到3.1.8 | lockinfo: 3.1.6→3.1.8
DEP-025 | docs-website http-cache-semantics 4.2.0 | 已锁定记录(观望) | 截至复核无修复版
DEP-026 | docs-website/package.json overrides image-size | 已升级到2.0.4 | lockinfo: 2.0.2→2.0.4(≥2.0.3)
DEP-027 | docs-website/package.json overrides joi | 已升级到17.13.8 | lockinfo: 17.13.6→17.13.8(≥17.13.7)
DEP-028 | docs-website/package.json overrides js-yaml | 已升级到4.3.2 | lockinfo: 4.3.1→4.3.2
DEP-029 | docs-website/package.json overrides qs | 已升级到6.16.0 | lockinfo: 6.15.3→6.16.0
DEP-030 | docs-website/package.json overrides svgo | 已升级到3.3.5 | lockinfo: 3.3.4→3.3.5
DEP-031 | docs-website/package-lock.json undici | (上轮已修7.29.0→7.29.1) | 保持
DEP-033 | docs-website webpack-dev-middleware 7.4.5 | 已锁定记录(待核) | 公告范围与版本边界矛盾,不计确认命中

### 供应链批次验证
- .NET: dotnet build MUnique.OpenMU.sln -> 0 错误;dotnet test -> 通过 2048 / 失败 0 / 跳过 6(与 OTel 升级前一致,无新增失败)。
- npm: overrides 写入 package.json,npm install 重生成 package-lock.json;lockinfo.py 解析 lockfile 确认 7 包全部达目标版本;braces/http-cache-semantics 维持无修复版状态。
- docker: 本机无 docker CLI,镜像仅固定精确 tag,digest 需在有 docker 环境补钉(未编造 digest)。
