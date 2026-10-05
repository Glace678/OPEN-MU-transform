# Shard B — MuMain 安全审计修复结果

工作根目录：D:\openmu自用（Windows，PowerShell）。所有改动保持未提交，未执行 git commit/push。
编译采用 ASCII 路径映射 subst Z: -> D:\openmu自用（原因见末尾摘要）。

## 编译验证（真实结果）

- cmake：D:\Microsoft Visual Studio\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe
- 配置：cmake -S Z:\MuMain -B Z:\MuMain\out\build\verify-b-ascii -G "Visual Studio 18 2026" -> CFG_EXIT=0
- 编译 Main：cmake --build Z:\MuMain\out\build\verify-b-ascii --config RelWithDebInfo --target Main -j 4 -> BUILD_EXIT=0
  - error C* / error LNK* / fatal error 计数 = 0（_audit\build-b3.log 复核 = 0）
  - 产物：out\build\verify-b-ascii\src\RelWithDebInfo\Main.exe，时间戳 2026-10-04 17:50:56，19.2 MB
- ClientLibrary：--target ClientLibrary -> CLIENTLIB_EXIT=0，产物 MUnique.Client.Library.dll（构建时一并 staging）
- 日志：_audit\build-b1.log / build-b2.log（中间检查点，均 0 error）、_audit\build-b3.log（最终，含 #40 CMake 改动后 reconfigure）
- 残留：仅 warning（C4819 代码页提示、LNK4098 libcmt 冲突），均为既有非致命告警，不阻断。

---

## 逐项结果（finding ID | 文件:行 | 状态 | 验证方式）

### A. MuMain 核心 #1-40
- MuMain#1..#11  | diff-B 既有修复        | 已修           | Main 编译通过(build-b3)
- MuMain#12      | FTPFileDownLoader.cpp:84/130 脚本名 basename 白名单(拒绝 ../ \ 盘符) | 已修 | Main 编译通过
- MuMain#13      | BannerInfo.cpp:60 URL basename 安全校验,非法即 return 1 | 已修 | Main 编译通过
- MuMain#14      | NewUICursedTempleSystem.cpp:1399 btPartyCount 0..MAX_PARTYS 否则丢弃 | 已修 | Main 编译通过
- MuMain#15      | NewUIChatInputBox.cpp RemoveChatHistory clamp 游标/复位 | 已修 | Main 编译通过
- MuMain#16      | Whisper.cpp wcscpy->wcsncpy(MAX_USERNAME_SIZE)+强制NUL; Clear sizeof(wchar_t) | 已修 | Main 编译通过
- MuMain#17      | Selection.cpp:267 地形索引 | 已修(diff-B)   | Main 编译通过
- MuMain#18      | FTPFileDownLoader.h:27 m_Break BOOL->std::atomic<bool> +<atomic> | 已修 | Main 编译通过
- MuMain#19      | diff-B 既有修复           | 已修           | Main 编译通过
- MuMain#20      | ZzzOpenglUtil.cpp:89 fread 返回值校验+记录短读 break+强制ID NUL | 已修 | Main 编译通过
- MuMain#21      | NewUISeigeWarfare.cpp:199 GuildMark[index] 前加 [0,MAX_MARKS) | 已修 | Main 编译通过
- MuMain#22      | diff-B 既有修复           | 已修           | Main 编译通过
- MuMain#23      | NewUIDuelWatchMainFrameWindow.cpp:210 敌方SD差值 DUEL_HERO->DUEL_ENEMY | 已修 | Main 编译通过
- MuMain#24      | NewUITextBox.h:48 ClearText 补 m_layoutDirty=true 失效布局缓存 | 已修 | Main 编译通过
  - 复审推翻旧"不支持"证据：报告行142 #24 Confirmed 0.98，位置 NewUITextBox.h:48/.cpp:64-65,111-126——ClearText 内联清空 m_vecText/m_sourceText 但未置 dirty，UpdateTextLayout()(.cpp:64) 缓存早退沿用旧行。已按建议落地。
- MuMain#25,#26,#27,#30,#33,#39 | diff-B 既有修复 | 已修     | Main 编译通过
- MuMain#28      | tools/ResxGen/ResxLoader.cs:78 TrySplitName 加组名白名单 ^[A-Za-z_][A-Za-z0-9_]*$ | 已修 | ResxGen 随 Main 构建调用通过
- MuMain#29      | tools/Localization/OpenCcBridge.mjs:18 realpathSync+containment 校验,逃逸 exit(1), require(pkgRoot) | 已修 | node 静态核对,opencc require 冒烟通过
- MuMain#31      | CMake NuGet restore       | 已记录         | 文档性(版本串已固定,哈希锁需离线策略,不阻断)
- MuMain#32      | ItemDataLoader.cpp:42 大小必须精确匹配 legacy/new 格式,否则 fclose+return false | 已修 | Main 编译通过
- MuMain#34      | MapManager.cpp:1754 IsCursedTemple(WorldActive)->直接用 iMap 范围 | 已修 | Main 编译通过
- MuMain#35      | tests/ui/InventoryTouchOperationsTests.cpp:79 failPickup 提前返回前 delete+置空 | 已修 | 测试目标随构建
- MuMain#36      | tests/core/RimeRuntimeSmoke.cpp:134 get_context 成功但无候选分支补 free_context | 已修 | 测试目标随构建
  - 复审推翻旧"不支持"证据：报告行156/157 #35/#36 均 Confirmed，给确切文件:行；#35 failPickup 真泄漏 new 对象、#36 漏 free_context，均确定性资源释放缺陷，已落地。
- MuMain#37      | ConnectionManager.ClientToServer.Custom.cs | 归 Shard A | 实际文件在 OpenMU
- MuMain#38      | tools/ResxGen/CppEmitter.cs:24 WriteIfChanged 保留原 BOM(检测 EF BB BF)+临时文件 Move 原子替换 | 已修 | ResxGen 随 Main 构建通过
- MuMain#40      | src/CMakeLists.txt:153 后追加 FILTER EXCLUDE REGEX 排除 ThirdParty/third_party/dependencies | 已修 | reconfigure+build-b3 通过,无重复编译
  - 复审推翻旧"不支持"证据：报告行161 #40 Confirmed 0.99，GLOB_RECURSE source/*.cpp 只排除 Platform 入口、未排除 ThirdParty/third_party，检出子模块后被重复编译；确为构建隔离问题，已追加路径段排除。

### B. R4-01..R4-35（全部已修，Main+ClientLibrary 编译通过）
- R4-01  ClientLibrary/ConnectionManager.ClientToServerFunctions.cs | helperData 非空+长度守卫
- R4-02  ZzzCharacter.cpp Queen Rainer 索引 >=0&&<MAX_CHARACTERS_CLIENT
- R4-03  ZzzCharacter.cpp LinkBone >=0&&<NumBones
- R4-04  ZzzCharacter.cpp HELL_BEGIN 条件优先级补括号
- R4-05  ZzzCharacter.cpp 骨骼检查 i<NumBones && !Dummy
- R4-06  ZzzCharacter.cpp vZX03/vZx04 VectorCopy 初始化
- R4-07  ZzzCharacter.cpp Blood Castle &&->||
- R4-08  ZzzCharacter.cpp Alice WEAKNESS 两处
- R4-09  ZzzInfomation.cpp DamageMin->DamageMax
- R4-10  ZzzInfomation.cpp OpenMonsterScript <MAX_MONSTER break
- R4-11  ZzzInterface.cpp ActionTarget 守卫
- R4-12  ZzzInterface.cpp ItemKey >=0&&<MAX_ITEMS
- R4-13  ZzzInterface.cpp memcpy->wcsncpy 255
- R4-14  ZzzInterface.cpp Party index 守卫
- R4-15  ZzzInterface.cpp wcscpy->wcsncpy 两处
- R4-16  ZzzInventory.cpp GetItemName 多路容量守卫
- R4-17  ZzzInventory.cpp Tooltip wcscat/wcscpy 64 容量
- R4-18  ZzzInventory.cpp CompareItem 反向分支
- R4-19  ZzzInventory.cpp IsPersonalShopBan 类型集合括号
- R4-20  ZzzInventory.cpp IsCorrectShopTitle 空指针+j<2047
- R4-21  ZzzInventory.cpp ClosePersonalShop wcsncpy
- R4-22  ZzzObject.cpp DeleteObjectTile UAF 缓存 next
- R4-23  ZzzObject.cpp _wfopen 空检查 return
- R4-24  ZzzObject.cpp vPos VectorCopy 初始化
- R4-25  ZzzOpenData.cpp OpenModel 容量化+AnimationCount<20
- R4-26  ZzzOpenData.cpp TERSIA break
- R4-27  ZzzOpenData.cpp BARNERT break
- R4-28  ZzzOpenData.cpp Fenrir Gold 修正
- R4-29  ZzzOpenData.cpp Gate +1->+i
- R4-30  PhysicsManager.cpp 布料零除守卫
- R4-31  ClassAttack.cpp SelectedCharacter 守卫
- R4-32  ZzzEffectJoint.cpp 激光 Distance>0.0001
- R4-33  ZzzEffectJoint.cpp RenderJoints 空指针前置
- R4-34  ZzzEffectJoint.cpp Chain Lightning 范围校验
- R4-35  ZzzEffectParticle.cpp ADV_SMOKE break

### C. Code2-MG-01..40
- MG-10  UIControls.cpp:2576 GetBgColor 补 return          | 已修 | Main 编译通过
- MG-19  NewUICommonMessageBox.cpp:1327 PartyKey 守卫      | 已修 | Main 编译通过
- MG-29  GMBattleCastle.cpp:475 ||->&&                     | 已修 | Main 编译通过
- MG-34  GMCrywolf1st.cpp:2656 TimeStart==true             | 已修 | Main 编译通过
- MG-36  GMEmpireGuardian1.cpp:2016 缺分号                 | 已修 | 编译阻断已解
- MG-37  GM_Kanturu_2nd.cpp:289/1279/1430 TargetCharacter  | 已修 | Main 编译通过
- MG-39  GM_Kanturu_3rd.cpp:1705/1724 fWidth->fHeight      | 已修 | Main 编译通过
- MG-40  GM_Raklion.cpp:1889/2141 TargetCharacter          | 已修 | Main 编译通过
- MG-07/15/17/18  diff-B 既有修复                           | 已修 | Main 编译通过
- MG-01  ZzzEffectParticle.cpp:8843 Owner 目标空指针 if(o->Target==nullptr)break | 已修 | Main 编译通过(build-mg4=0)
- MG-02  ZzzBMD.cpp:3089 BMDReader Read<T>/ReadBytes 边界保护(截断文件不越界 memcpy,零填充) | 已修 | Main 编译通过
- MG-03  ZzzBMD.cpp:3358 Save2 ModelName[64]->[260]+wcsncat 有界拼接(栈溢出消除) | 已修 | Main 编译通过
- MG-04  ZzzBMD.cpp:1943 渲染三角索引 Vertex/TexCoord/Normal [0,Num) 守卫(2 处渲染路径) | 已修 | Main 编译通过
- MG-05  RHI_GL.cpp BindVertex/IndexBuffer IsUboReservation(handle)return | 已修 | Main 编译通过
- MG-06  GlobalBitmap.cpp OpenTga 22字节头+nx*ny*4 载荷长度校验 | 已修 | Main 编译通过
- MG-08  ZzzLodTerrain.cpp SaveTerrainMapping fp==NULL return false | 已修 | Main 编译通过
- MG-09  UIControls.cpp SlideHelp iNumber[0,32) | 已修 | Main 编译通过
- MG-11  UIControls.h GetColumnPos_x 循环上界 clamp 4 | 已修 | Main 编译通过
- MG-12  UIGateKeeper step/max 非负净化+EnteranceFee 饱和算术 | 已修 | Main 编译通过
- MG-13  UIWindows.cpp 0x0D notice 分发前校验 Size>=sizeof(FS_CHAT_TEXT) | 已修 | Main 编译通过
- MG-14  UIWindows.cpp 0x02 userlist header+Count*record<=Size | 已修 | Main 编译通过
- MG-16  UIWindows.cpp 好友菜单 iter==end return | 已修 | Main 编译通过
- MG-20  NewUICommonMessageBox.cpp:126 SetText nullptr&&wcslen>0 | 已修 | Main 编译通过
- MG-21  NewUICustomMessageBox.cpp memcpy/memcmp m_iInputLimit*sizeof(wchar_t) | 已修 | Main 编译通过
- MG-22  NewUICustomMessageBox.cpp SeparateText 首字超宽强制消费一个码点(4 处同构函数全修) | 已修 | Main 编译通过
- MG-23  NewUICustomMessageBox.cpp CProgressMsgBox/CursedTemple 空 Release 补 m_MsgDataList SAFE_DELETE | 已修 | Main 编译通过
- MG-24  NewUIMessageBox.cpp:70 GetPriority return m_fPriority 取代硬编码 8.f | 已修 | Main 编译通过
- MG-25  NewUIMuHelper.cpp:840 iSlotIndex[0,6) | 已修 | Main 编译通过
- MG-26  NewUISystem.cpp:563 SAFE_DELETE(m_pNewStorageInventoryExt) | 已修 | Main 编译通过
- MG-27  NewUISystem.cpp:173 if(m_pNewUIMng)RemoveAllUIObjs 判空 | 已修 | Main 编译通过
- MG-28  NewUIOptionWindow.cpp:2785 GetModuleFileNameW>=MAX_PATH 截断检测 | 已修 | Main 编译通过
- MG-30  GMBattleCastle.cpp:318 循环上界快照 size() | 已修 | Main 编译通过
- MG-31  GMBattleCastle.cpp:103 else 补 OpenTerrainLight(FileName) | 已修 | Main 编译通过
- MG-32  GMCrywolf1st.cpp Check_AltarState Num[1,5]/Set_Message_Box Num[0,2) ObjNum[0,5) | 已修 | Main 编译通过
- MG-33  GMCrywolf1st.cpp Set_WorldRank Rank>=5||szHeroName==nullptr return | 已修 | Main 编译通过
- MG-38  GM_Kanturu_3rd.cpp CreateCharacter 后 4 处 if(!c)break | 已修 | Main 编译通过
- MG-35  GMEmpireGuardian1.cpp TargetCharacter [0,MAX_CHARACTERS_CLIENT) 守卫 3 处(641 continue->switch case 改 break;957/987 break) | 已修 | Main 编译通过(build-mg3/mg4)

### D. 平台 / 批处理
- PLAT-M01 windows-build.yml:76 libjpeg-turbo --branch 3.1.3   | 已合规
- PLAT-M02 claude.yml                                           | 已修(diff-B)
- PLAT-M03 linux-build.yml:74 ldd not found                     | 已修(diff-B)
- PLAT-M04 build_*.bat exit /b %ERRORLEVEL%                     | 已修
- PLAT-M05 linux-build.yml:82 continue-on-error                  | 已合规
- NEW-MUMAIN-02 build_*.bat 传播 configure/build 失败状态       | 已修

### E. Code5 #5-9
- Code5#5..#9  实际文件在 OpenMU/src/Network/...               | 归 Shard A

### F. 第三方 / 供应链
- VEND#1 TurboJPEG OZJ 越界写                                   | 已修(diff-B) | 链接通过
- VEND#2 SDL_mixer 恢复逻辑                                     | 已修(diff-B) | lib 构建成功
- VEND#3 MSVC 运行时 DLL 缺失                                   | 已记录 | staging 已复制
- VEND#4=DEP-032 Localization undici 6.28.0 -> 6.29.0           | 已修 | package.json overrides undici:6.29.0; npm ls=6.29.0(overridden); opencc require 冒烟通过
- VEND#5 stb_truetype 争议                                      | 已记录
- VEND#6 libjpeg-turbo SBOM                                     | 已记录 | 3.1.3 已固定
- VEND#7 license/NOTICE 缺口                                    | 已修 | SDL/SDL_mixer/imgui LICENSE.txt 树内已存在;doctest 缺,已从上游 raw.githubusercontent.com/doctest/doctest@v2.4.11/LICENSE.txt 获取 MIT 全文写入 tests/third_party/doctest/LICENSE(未编造)
- DEP-002-B .github actions 钉完整 SHA                          | 已修 | git ls-remote 真实解析,9 处 uses 全部替换

DEP-002-B 实际 SHA（2026-10-04 git ls-remote github.com 解析，禁止编造）：
- actions/checkout@v4          -> 11d5960a326750d5838078e36cf38b85af677262
- actions/setup-dotnet@v4      -> 67a3573c9a986a3f9c594539f4ab511d57bb3ce9
- actions/upload-artifact@v4   -> ea165f8d65b6e75b540449e92b4886f43607fa02
- actions/cache@v4             -> 0057852bfaa89a56745cba8c7296529d2fc39830
- anthropics/claude-code-action@v1 -> 1bc23c05956f182d538474fec6c62d9c830b47e9
- 覆盖 claude.yml / linux-build.yml / windows-build.yml 共 9 处 uses。

---

## Shard B 修复摘要

关键改动（本轮续作新增落地，全部经 build-b3.log 真编译）：
1. MuMain 核心第一优先 #12/#13/#14/#15/#16/#18/#20/#21/#23/#28/#29/#32/#34 全部从"遗留"改为代码加固：路径穿越白名单、数组索引守卫、atomic 中断标志、配置读取长度校验、组名/模块路径 canonical containment、格式精确长度门控、iMap 谓词、编码保留+原子写。
2. 复审纠错并真正落地 #24/#35/#36/#40（此前误标"不支持"）：NewUITextBox 布局缓存 dirty、测试 new 泄漏 delete、Rime free_context 补分支、CMake glob 排除 vendored 路径段。
3. 供应链 DEP-002-B：9 处 GitHub Actions 钉到 git ls-remote 真实解析的完整 SHA；VEND#4=DEP-032：undici 经 npm override 6.28.0->6.29.0，opencc 工具链冒烟通过。

编译命令与结果：
- configure: cmake -S Z:\MuMain -B Z:\MuMain\out\build\verify-b-ascii -G "Visual Studio 18 2026" -> CFG_EXIT=0
- build Main:  --target Main -j 4 -> BUILD_EXIT=0，error C/LNK/fatal=0，产出 Main.exe(19.2MB, 17:50:56)
- build ClientLibrary: --target ClientLibrary -> 0，产出 MUnique.Client.Library.dll

环境问题（已解）：路径含中文致 SDL PCH GBK/UTF-8 转码阻断，用 subst Z: ASCII 盘符规避。

覆盖与遗留：Shard=B 全部 135 ID 已逐项标注。第二轮续作新落地 Code2-MG 全部 27 项(MG-01/02/03/04/05/06/08/09/11/12/13/14/16/20/21/22/23/24/25/26/27/28/30/31/32/33/38) + MG-35(GMEmpireGuardian1 TargetCharacter 边界 3 处) + VEND#7(doctest LICENSE 上游获取)。
最终编译：build-mg4.log BUILD_EXIT=0，error C/LNK/fatal=0，Main.exe 时间戳 2026-10-04 18:13:38，19.2MB。统计：已落地代码/文档加固约 128 项；归 Shard A(文件在 OpenMU)：#37、Code5#5-9；仅记录(版本固定/CI范围/合规台账)：#31、VEND#3/#5/#6。无无据遗留项。
