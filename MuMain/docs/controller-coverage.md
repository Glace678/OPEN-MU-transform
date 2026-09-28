# Controller coverage audit

This file is generated from `docs/controller-coverage.csv`. Run `tools/ControllerCoverage/Test-ControllerCoverage.ps1 -UpdateGenerated` after an intentional registry update.

Static coverage means that a controller route is documented and reachable through the common semantic-input or virtual-pointer layer. It does not replace runtime replay or real-controller validation.

## Inventory

| Measure | Count |
| --- | ---: |
| INTERFACE_LIST player interfaces | 85 |
| AddUIObj registered interface identifiers | 80 |
| AddUIObj source registration sites | 81 |
| CUIMng fixed legacy windows | 10 |
| UIWINDOWSTYPE dynamic window types | 10 |
| Legacy scenes/workflows | 12 |
| Non-PacketFunctions Send* helper sites | 38 |
| Client Send* entrypoints | 210 |
| Files with mouse-button dependencies | 78 |
| Mouse-button dependency references | 682 |
| Files with text-input dependencies | 46 |
| Text-input dependency references | 926 |

## Network action families

| Family | `Send*` entrypoints |
| --- | ---: |
| Automation | 1 |
| Character | 15 |
| Combat | 16 |
| Inventory | 19 |
| Navigation | 10 |
| Protocol | 13 |
| QuestEvent | 53 |
| Session | 15 |
| ShopTrade | 21 |
| Social | 47 |

## Status

| Implementation status | Entries |
| --- | ---: |
| ControllerKeyboard+PointerFallback | 15 |
| ImGuiNativeNavigation | 1 |
| NativeSemanticActions | 2 |
| NativeSemanticActions+PointerFallback | 1 |
| PointerFallback | 39 |
| PointerFallback+ControllerKeyboard | 17 |
| ReadOnlyReachable | 10 |
| SemanticKeys+ControllerKeyboard | 1 |
| SemanticKeys+ControllerKeyboard+PointerFallback | 10 |
| SemanticKeys+PointerFallback | 21 |

| Validation status | Entries |
| --- | ---: |
| PendingRuntime | 117 |

## Coverage matrix

| ID | Kind | Category | Player workflow | Controller route | Text entry | Haptic confirmation | Implementation | Validation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SCENE_LOGIN | LegacyScene | Session | Login | Left stick or D-pad focus; A activate; B cancel; View next field; Menu submit; right-stick pointer fallback | Controller keyboard in ABC mode for account and password | Submit immediate; login result is server-confirmed | SemanticKeys+ControllerKeyboard | PendingRuntime |
| SCENE_SERVER_LIST | LegacyScene | Session | Server selection | D-pad or left stick focus; A choose; B back; right-stick pointer fallback | N/A | Focus and confirm immediate; connection result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| SCENE_CHARACTER_SELECT | LegacyScene | Character | Character selection | D-pad or left stick focus; A select or enter; X secondary/delete; Y details/create; B back; pointer fallback | Controller keyboard for delete confirmation or PIN when requested | Selection immediate; creation/deletion/login result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| SCENE_CHARACTER_CREATE_DELETE | LegacyScene | Character | Character creation and deletion | D-pad or left stick focus; shoulder buttons change class; A confirm; B cancel; View next field; Menu submit | ABC controller keyboard for character names; numeric or ABC mode for security fields | Request immediate; create/delete result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| WORLD_MOVEMENT_PICKUP | Gameplay | World | Movement and interaction | Left stick move; L3 auto-move; A interact or pick up; R3 cycle target; View map; B cancel; right stick world pointer | N/A | Pickup and item result server-confirmed; target and cancel immediate | NativeSemanticActions | PendingRuntime |
| WORLD_COMBAT_SKILLS | Gameplay | Combat | Combat and skills | X basic attack; RT current skill; LT target lock; R3 next target; LB or RB previous/next skill; D-pad hotkeys | N/A | Hit, critical, heavy hit, damage and death are server-confirmed | NativeSemanticActions | PendingRuntime |
| WORLD_PET_POTIONS_HELPER | Gameplay | Combat | Pet, consumables and automation | D-pad quick items; assigned pet commands use the existing skill selection plus RT path; L3 auto-move; Menu opens system menu; Y opens shortcut keyboard | N/A | Item use and helper result server-confirmed where available | SemanticKeys+PointerFallback | PendingRuntime |
| WORKFLOW_SHORTCUT_KEYBOARD | LegacyWorkflow | System | Keyboard-only commands and modifiers | Y in world or View plus Start elsewhere; D-pad or left stick selection; A activate; B clear and close; LT Ctrl and RT Shift in UI | Dedicated command keyboard; text fields retain the text-entry keyboard | Low-priority input acknowledgement; focus and confirm feedback; command results remain server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| WORKFLOW_GUILD_STORAGE | LegacyWorkflow | Inventory | Guild warehouse | D-pad or left stick grid focus; A pick/place; hold A drag; X money/secondary; Y details; B close; right-stick pointer fallback | Numeric controller keyboard for amounts or security fields | Move and money results server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| WORKFLOW_CHAT_MAIL | LegacyWorkflow | Social | Chat, whisper and mail | A focus; View next field; Menu submit; shoulder buttons page candidates or lists; B back; pointer fallback | Controller keyboard with Rime simplified-pinyin candidates | Send immediate; failure and mail result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| WORKFLOW_RECONNECT_ERRORS | LegacyWorkflow | Session | Reconnect and error dialogs | D-pad focus; A confirm or retry; B cancel or back; right-stick pointer fallback | Controller keyboard when a dialog requests credentials or PIN | Confirm/cancel immediate; reconnect result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| EDITOR_IMGUI | LegacyWorkflow | Editor | MuEditor gamepad navigation | ImGui NavEnableGamepad; D-pad or left stick focus; A activate; B cancel; LB/RB tabs; shared active SDL gamepad | Editor text fields use ImGui input and the operating-system keyboard; game client screen keyboard is not injected into editor builds | Focus and setting changes use editor UI feedback; gameplay semantic haptics are not emitted | ImGuiNativeNavigation | PendingRuntime |
| LEGACY_m_MsgWin | LegacyWindow | System | Legacy message window | D-pad or left stick focus; A confirm; X alternate; B cancel; right-stick pointer fallback | Controller keyboard for amount, name, password or PIN message variants | Confirm/cancel immediate; requested operation server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| LEGACY_m_SysMenuWin | LegacyWindow | System | Legacy system menu | D-pad or left stick focus; A choose; B resume; right-stick pointer fallback | N/A | Focus and confirm/cancel immediate | SemanticKeys+PointerFallback | PendingRuntime |
| LEGACY_m_OptionWin | LegacyWindow | System | Legacy options | D-pad or left stick focus; A toggle; left/right adjust; B cancel; pointer fallback | N/A | Setting-step and confirm/cancel immediate | SemanticKeys+PointerFallback | PendingRuntime |
| LEGACY_m_LoginMainWin | LegacyWindow | Session | Login main menu | D-pad or left stick focus; A choose; B back; right-stick pointer fallback | N/A | Focus and confirm/cancel immediate | SemanticKeys+PointerFallback | PendingRuntime |
| LEGACY_m_ServerSelWin | LegacyWindow | Session | Legacy server selection | D-pad or left stick focus; A select; B back; right-stick pointer fallback | N/A | Selection immediate; connection result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| LEGACY_m_LoginWin | LegacyWindow | Session | Legacy credential entry | D-pad or left stick focus; A activate; View next field; Menu submit; B cancel; pointer fallback | Controller keyboard in ABC mode for account and password | Submit immediate; login result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| LEGACY_m_CreditWin | LegacyWindow | System | Credits | D-pad scroll; A continue; B close; right-stick pointer fallback | N/A | Page/close immediate | PointerFallback | PendingRuntime |
| LEGACY_m_ServerMsgWin | LegacyWindow | Session | Server messages | D-pad scroll; A acknowledge; B close expanded view; pointer fallback | N/A | Acknowledge immediate | PointerFallback | PendingRuntime |
| LEGACY_m_CharSelMainWin | LegacyWindow | Character | Legacy character selection | D-pad or left stick focus; A enter/select; X delete; Y create/details; B back; pointer fallback | Controller keyboard for deletion PIN/password when requested | Selection immediate; enter/delete result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| LEGACY_m_CharMakeWin | LegacyWindow | Character | Legacy character creation | D-pad or left stick focus; LB/RB class; A create; B cancel; pointer fallback | Controller keyboard with Chinese name mode | Create result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_CHAT | DynamicWindow | Social | Private chat room | D-pad or left stick focus; A activate/select; View next field; Menu send; X invite; B close; pointer fallback | Controller keyboard with simplified-pinyin candidates | Send immediate; room and invitation result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_CHAT_READY | DynamicWindow | Social | Pending private chat room | D-pad or left stick focus; A confirm; X invite; B cancel; pointer fallback | Controller keyboard when room input becomes active | Room connection result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_FRIENDMAIN | DynamicWindow | Social | Legacy friends and mail hub | D-pad or left stick focus; A primary; X secondary/delete; Y details; LB/RB tabs; B close; pointer fallback | Controller keyboard for names, chat and mail | Friend, room and mail results server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_TEXTINPUT | DynamicWindow | System | Generic text input | A focus; View next field; Menu submit; B cancel; pointer fallback | Controller keyboard chooses Chinese, ABC or numeric mode from focused field | Confirm immediate; requested operation server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_QUESTION | DynamicWindow | System | Question dialog | D-pad or left stick focus; A confirm; X reject; B cancel; pointer fallback | N/A | Confirm/cancel immediate; requested operation server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_READLETTER | DynamicWindow | Social | Read mail | D-pad scroll/focus; A primary; X delete; Y reply/details; LB/RB previous/next; B close; pointer fallback | Controller keyboard when replying | Read/delete result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_WRITELETTER | DynamicWindow | Social | Compose mail | D-pad or left stick focus; View next field; Menu send; LB/RB pose; B cancel; pointer fallback | Controller keyboard with simplified-pinyin candidates | Send result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_OK | DynamicWindow | System | Information dialog | A acknowledge; B close; pointer fallback | N/A | Acknowledge immediate | SemanticKeys+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_QUESTION_FORCE | DynamicWindow | System | Blocking question dialog | D-pad focus; A confirm; X reject; B maps to explicit reject only; pointer fallback | N/A | Confirm/cancel immediate; requested operation server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| DYNAMIC_UIWNDTYPE_OK_FORCE | DynamicWindow | System | Blocking information dialog | A acknowledge; pointer fallback; B only when dialog permits close | N/A | Acknowledge immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_FRIEND | NewUI | Social | Friends, whisper and mail | D-pad or left stick focus; A primary; X secondary/delete; Y details; LB/RB page; B close; pointer fallback | Controller keyboard for player names, chat and mail | UI immediate; add/delete/invite and mail results server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_MOVEMAP | NewUI | Navigation | Move command | D-pad or left stick focus; A warp; Y details; LB/RB page; B close; pointer fallback | N/A | Selection immediate; warp result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_PARTY | NewUI | Social | Party member panel | D-pad or left stick focus; A select; X secondary; Y details; B close; pointer fallback | N/A | Focus immediate; party result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_MYQUEST | NewUI | Quest | Quest log | D-pad or left stick focus; A select; X cancel/secondary; Y details; LB/RB page; B close; pointer fallback | N/A | UI immediate; quest change result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_NPCQUEST | NewUI | Quest | Legacy NPC quest | D-pad or left stick focus; A accept/proceed; X decline; Y details; B close; pointer fallback | N/A | Quest result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_GUILDINFO | NewUI | Guild | Guild and alliance | D-pad or left stick focus; A primary; X role/delete; Y details; LB/RB tabs; B close; pointer fallback | Controller keyboard for names, notices and security code | UI immediate; guild changes server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_TRADE | NewUI | Trade | Player trade | D-pad or left stick grid focus; A pick/place; hold A drag; X set money/secondary; Y details; B cancel; pointer fallback | Numeric controller keyboard for money | Trade result and failures server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_STORAGE | NewUI | Inventory | Warehouse | D-pad or left stick grid focus; A pick/place; hold A drag; X money/secondary; Y details; B close; pointer fallback | Numeric keyboard for amount/PIN; ABC keyboard for account password | Move, money and vault protection result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_STORAGE_EXT | NewUI | Inventory | Extended warehouse | D-pad or left stick grid focus; A pick/place; hold A drag; LB/RB page; Y details; B close; pointer fallback | N/A | Item move result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_MIXINVENTORY | NewUI | Crafting | Chaos machine and synthesis | D-pad or left stick grid focus; A pick/place; X mix/secondary; Y details; B cancel; pointer fallback | Numeric keyboard where quantity is requested | Mix success/failure server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_COMMAND | NewUI | Social | Character command menu | D-pad or left stick focus; A primary; X alternate; B close; pointer fallback | Controller keyboard when command needs a name | Command result server-confirmed where applicable | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_PET | NewUI | Character | Pet information and commands | D-pad or left stick focus; A select; X command; Y details; B close; pointer fallback | N/A | Pet command and info server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_NPCSHOP | NewUI | Shop | NPC shop | D-pad or left stick grid focus; A buy/pick; X sell/repair; Y details; B close; pointer fallback | Numeric keyboard for quantity where supported | Buy, sell and repair result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_INVENTORY | NewUI | Inventory | Backpack and equipment | D-pad or left stick grid focus; A pick/place/use; hold A drag; X secondary/split; Y details; B close; pointer fallback | Numeric keyboard for stack split | Item move/use/drop/repair result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_INVENTORY_EXT | NewUI | Inventory | Extended backpack | D-pad or left stick grid focus; A pick/place/use; hold A drag; X split; Y details; LB/RB page; B close; pointer fallback | Numeric keyboard for stack split | Item move/use/drop result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_MYSHOP_INVENTORY | NewUI | Shop | Personal shop setup | D-pad or left stick grid focus; A pick/place; X price/remove; Y details; B close; pointer fallback | Numeric keyboard for price; Chinese keyboard for shop name | Open, close and price result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_PURCHASESHOP_INVENTORY | NewUI | Shop | Other player's personal shop | D-pad or left stick grid focus; A buy; Y details; LB/RB page; B close; pointer fallback | N/A | Purchase success/failure server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CHARACTER | NewUI | Character | Character stats | D-pad or left stick focus; A add; X alternate increment; Y details; LB/RB tabs; B close; pointer fallback | Numeric keyboard for bulk point allocation where available | Stat allocation result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_NPCBREEDER | NewUI | NPC | Pet breeder legacy dialog | D-pad or left stick focus; A confirm; X secondary; B cancel; pointer fallback | N/A | Operation result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_SERVERDIVISION | NewUI | Session | Server division legacy dialog | D-pad or left stick focus; A select; B back; pointer fallback | N/A | Connection result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_DEVILSQUARE | NewUI | Event | Devil Square entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_BLOODCASTLE | NewUI | Event | Blood Castle entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_NPCGUILDMASTER | NewUI | Guild | Guild creation | D-pad or left stick focus; A draw/select/confirm; X erase/secondary; B cancel; pointer fallback | Controller keyboard for guild name | Create result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_GUARDSMAN | NewUI | Castle | Castle guardsman | D-pad or left stick focus; A confirm; X secondary; Y details; B close; pointer fallback | Numeric keyboard for mark count where required | Castle operation result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_SENATUS | NewUI | Castle | Castle management senate | D-pad or left stick focus; A confirm; X secondary; Y details; LB/RB tabs; B close; pointer fallback | Numeric keyboard for tax, amount or upgrade values | Castle operation result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_GATEKEEPER | NewUI | Castle | Castle gatekeeper | D-pad or left stick focus; A confirm/enter; X setting; Y details; B close; pointer fallback | Numeric keyboard for fee where requested | Setting/entry result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_GATESWITCH | NewUI | Castle | Castle gate switch | D-pad or left stick focus; A toggle; B cancel; pointer fallback | N/A | Toggle result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CATAPULT | NewUI | Castle | Castle catapult | D-pad or left stick focus; A fire; Y target details; B cancel; pointer fallback | N/A | Fire result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_REFINERY | NewUI | Crafting | Refinery legacy dialog | D-pad or left stick focus; A pick/place/confirm; X refine; Y details; B cancel; pointer fallback | N/A | Refinery result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_REFINERYINFO | NewUI | Crafting | Refinery information | D-pad scroll; A acknowledge; B close; pointer fallback | N/A | Confirm/cancel immediate | PointerFallback | PendingRuntime |
| INTERFACE_KANTURU2ND_ENTERNPC | NewUI | Event | Kanturu entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CURSEDTEMPLE_NPC | NewUI | Event | Cursed Temple entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CURSEDTEMPLE_GAMESYSTEM | NewUI | Event | Cursed Temple match | D-pad event skills; A objective interaction; RT skill; R3 target; pointer fallback | N/A | Event skill and combat result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_CURSEDTEMPLE_RESULT | NewUI | Event | Cursed Temple result | D-pad scroll; A acknowledge/reward; B close; pointer fallback | N/A | Reward result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CHATINPUTBOX | NewUI | Social | Chat input | A focus; View next field; Menu send; shoulder buttons candidates/history; B cancel; pointer fallback | Controller keyboard with simplified-pinyin candidates | Send immediate; server rejection uses failure feedback | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_WINDOW_MENU | NewUI | System | System menu | D-pad or left stick focus; A open; B close; pointer fallback | N/A | Focus, confirm and cancel immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_OPTION | NewUI | System | Settings | D-pad or left stick focus; A toggle; left/right adjust; X reset/secondary; Y test/detail; LB/RB tabs; B back; pointer fallback | N/A | Setting-step, confirm/cancel and short/long rumble test immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_HELP | NewUI | System | Help | D-pad scroll/focus; A topic; LB/RB page; B close; pointer fallback | N/A | Focus and close immediate | PointerFallback | PendingRuntime |
| INTERFACE_ITEM_EXPLANATION | NewUI | Inventory | Item tooltip | Y details; D-pad scroll where applicable; B close tooltip | N/A | Focus immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_SETITEM_EXPLANATION | NewUI | Inventory | Set item tooltip | Y details; D-pad scroll; B close tooltip | N/A | Focus immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_QUICK_COMMAND | NewUI | Social | Quick target command | D-pad or left stick focus; A execute; X alternate; B close; pointer fallback | N/A | Command result server-confirmed where applicable | PointerFallback | PendingRuntime |
| INTERFACE_KANTURU_INFO | NewUI | Event | Kanturu event information | D-pad scroll; A acknowledge; B close; pointer fallback | N/A | Confirm/cancel immediate | PointerFallback | PendingRuntime |
| INTERFACE_CHATLOGWINDOW | NewUI | Social | Chat log | D-pad scroll/focus; LB/RB channels; A select; X filter; B close; pointer fallback | Controller keyboard when opening reply | Focus immediate | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_PARTY_INFO_WINDOW | NewUI | Social | Party list | D-pad or left stick focus; A select; X kick/leave; Y details; B close; pointer fallback | N/A | Party result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_BLOODCASTLE_TIME | NewUI | Event | Blood Castle HUD | No action required; B closes optional detail; pointer fallback | N/A | No action haptic; major event result server-confirmed | ReadOnlyReachable | PendingRuntime |
| INTERFACE_CHAOSCASTLE_TIME | NewUI | Event | Chaos Castle HUD | No action required; B closes optional detail; pointer fallback | N/A | No action haptic; major event result server-confirmed | ReadOnlyReachable | PendingRuntime |
| INTERFACE_BATTLE_SOCCER_SCORE | NewUI | Event | Battle Soccer HUD | No action required; B closes optional detail; pointer fallback | N/A | No action haptic; major event result server-confirmed | ReadOnlyReachable | PendingRuntime |
| INTERFACE_SLIDEWINDOW | NewUI | System | Tutorial slides | D-pad scroll; LB/RB previous/next; A continue; B close; pointer fallback | N/A | Page, confirm and cancel immediate | PointerFallback | PendingRuntime |
| INTERFACE_HERO_POSITION_INFO | NewUI | Navigation | Position HUD | No action required; View opens map | N/A | No action haptic | ReadOnlyReachable | PendingRuntime |
| INTERFACE_MESSAGEBOX | NewUI | System | Message and confirmation dialogs | D-pad or left stick focus; A confirm; X secondary; B cancel; pointer fallback | Controller keyboard selected by requested field type | Confirm/cancel immediate; business result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_DUEL_WINDOW | NewUI | Combat | Duel request and state | D-pad or left stick focus; A accept; X reject/stop; Y details; B close; pointer fallback | N/A | Duel request/result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_CRYWOLF | NewUI | Event | Crywolf event | D-pad or left stick focus; A contract/select; Y details; B close; pointer fallback | N/A | Contract and event result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_NAME_WINDOW | NewUI | System | Name entry dialog | A focus; View next field; Menu submit; B cancel; pointer fallback | Controller keyboard with context-selected Chinese or ABC mode | Confirm immediate; business result server-confirmed | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_SIEGEWARFARE | NewUI | Castle | Castle Siege HUD | D-pad command; A place/confirm; X alternate command; Y details; B close; pointer fallback | N/A | Command and event result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_MAINFRAME | NewUI | HUD | Main HUD | D-pad hotkeys; LB/RB skill; A context; X basic attack; RT skill; View map; Menu system menu; pointer fallback | N/A | Action-dependent immediate or server-confirmed feedback | NativeSemanticActions+PointerFallback | PendingRuntime |
| INTERFACE_SKILL_LIST | NewUI | Combat | Skill list | D-pad or left stick focus; A select/use; X assign; Y details; LB/RB page; B close; pointer fallback | N/A | Selection immediate; skill result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_ITEM_ENDURANCE_INFO | NewUI | Inventory | Durability information | D-pad focus; Y details; B close; pointer fallback | N/A | Focus immediate | ReadOnlyReachable | PendingRuntime |
| INTERFACE_BUFF_WINDOW | NewUI | Combat | Buff list | D-pad focus; Y details; B close; pointer fallback | N/A | Focus immediate | ReadOnlyReachable | PendingRuntime |
| INTERFACE_MASTER_LEVEL | NewUI | Character | Master skill tree | D-pad or left stick focus; A allocate/select; X alternate; Y details; LB/RB page; B close; pointer fallback | Numeric keyboard for bulk points where available | Allocation result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_GOLD_BOWMAN | NewUI | Event | Gold Bowman event | D-pad or left stick focus; A submit/confirm; Y details; B close; pointer fallback | N/A | Reward result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_GOLD_BOWMAN_LENA | NewUI | Event | Lena event | D-pad or left stick focus; A submit/confirm; Y details; B close; pointer fallback | N/A | Reward result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_LUCKYCOIN_REGISTRATION | NewUI | Event | Lucky Coin registration | D-pad or left stick focus; A confirm; X amount; B cancel; pointer fallback | Numeric controller keyboard for count | Registration result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_EXCHANGE_LUCKYCOIN | NewUI | Event | Lucky Coin exchange | D-pad or left stick focus; A exchange; X amount; Y details; B cancel; pointer fallback | Numeric controller keyboard for count | Exchange result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_DUELWATCH | NewUI | Combat | Duel spectator | D-pad or left stick focus; A join/watch; X leave; Y details; B close; pointer fallback | N/A | Channel result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_DUELWATCH_MAINFRAME | NewUI | Combat | Duel spectator HUD | A primary spectator action; B leave/close; pointer fallback | N/A | Leave/result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_DUELWATCH_USERLIST | NewUI | Combat | Duel spectator list | D-pad or left stick focus; A select; Y details; B close; pointer fallback | N/A | Selection immediate | PointerFallback | PendingRuntime |
| INTERFACE_INGAMESHOP | NewUI | CashShop | Cash shop | D-pad or left stick focus; A buy/use; X gift/delete; Y details; LB/RB page; B close; right-stick pointer and A for recharge/exchange | Controller keyboard for recipient and gift text | Purchase, gift, claim, delete and local credit exchange results server-confirmed; physical motor feedback unverified | ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_DOPPELGANGER_NPC | NewUI | Event | Doppelganger entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_DOPPELGANGER_FRAME | NewUI | Event | Doppelganger HUD | No action required; B closes optional detail; pointer fallback | N/A | Major event result server-confirmed | ReadOnlyReachable | PendingRuntime |
| INTERFACE_QUEST_PROGRESS | NewUI | Quest | Current quest dialogue | D-pad or left stick focus; A proceed; X cancel; Y details; B close; pointer fallback | N/A | Quest result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_QUEST_PROGRESS_ETC | NewUI | Quest | Quest progress alternate | D-pad or left stick focus; A proceed; X cancel; Y details; B close; pointer fallback | N/A | Quest result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_EMPIREGUARDIAN_NPC | NewUI | Event | Empire Guardian entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_EMPIREGUARDIAN_TIMER | NewUI | Event | Empire Guardian HUD | No action required; B closes optional detail; pointer fallback | N/A | Major event result server-confirmed | ReadOnlyReachable | PendingRuntime |
| INTERFACE_MINI_MAP | NewUI | Navigation | Mini-map | View toggle; D-pad or left stick pan/focus; A marker; Y details; B close; pointer fallback | N/A | Selection immediate; command result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_NPC_DIALOGUE | NewUI | NPC | NPC dialogue | D-pad or left stick focus; A choose; X secondary; Y details; B close; pointer fallback | Controller keyboard only when response requests text/amount | Business result server-confirmed | PointerFallback+ControllerKeyboard | PendingRuntime |
| INTERFACE_GENSRANKING | NewUI | Social | Gens faction and ranking | D-pad or left stick focus; A confirm; X leave; Y details; LB/RB page; B close; pointer fallback | N/A | Join, leave and reward result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_UNITEDMARKETPLACE_NPC_JULIA | NewUI | Navigation | United Market Place entry | D-pad or left stick focus; A enter; Y details; B cancel; pointer fallback | N/A | Entry result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_LUCKYITEMWND | NewUI | Inventory | Lucky item window | D-pad or left stick focus; A select/use; X secondary; Y details; B close; pointer fallback | N/A | Item result server-confirmed | PointerFallback | PendingRuntime |
| INTERFACE_HOTKEY | NewUI | Combat | Hotkey bar | D-pad direct-use; LB/RB skill page; A select; X assign; Y details; pointer fallback | N/A | Use result server-confirmed | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_ITEM_TOOLTIP | NewUI | Inventory | Common item tooltip | Y details; D-pad scroll where applicable; B close | N/A | Focus immediate | ReadOnlyReachable | PendingRuntime |
| INTERFACE_MUHELPER | NewUI | Automation | MU Helper settings | D-pad or left stick focus; A toggle/select; left/right adjust; X reset/secondary; LB/RB tabs; B close; pointer fallback | Controller keyboard for numeric thresholds where offered | Setting immediate; save/status result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_MUHELPER_EXT | NewUI | Automation | MU Helper extended settings | D-pad or left stick focus; A toggle/select; left/right adjust; X reset; LB/RB tabs; B close; pointer fallback | Controller keyboard for numeric thresholds where offered | Setting immediate; save result server-confirmed | SemanticKeys+ControllerKeyboard+PointerFallback | PendingRuntime |
| INTERFACE_MUHELPER_SKILL_LIST | NewUI | Automation | MU Helper skill picker | D-pad or left stick focus; A assign; Y details; LB/RB page; B close; pointer fallback | N/A | Selection immediate | SemanticKeys+PointerFallback | PendingRuntime |
| INTERFACE_SYSTEMLOGWINDOW | NewUI | HUD | System log | D-pad scroll; B closes optional expanded view; pointer fallback | N/A | No action haptic | ReadOnlyReachable | PendingRuntime |
| INTERFACE_COMMAND_LIST | NewUI | Social | Chat command list | D-pad or left stick focus; A insert; Y details; B close; pointer fallback | Controller keyboard continues command parameters | Focus/insert immediate | ControllerKeyboard+PointerFallback | PendingRuntime |

## Acceptance gate

- The audit fails when a concrete `INTERFACE_LIST` value is absent from the CSV.
- The audit fails when an `AddUIObj` registration is absent from the CSV.
- The audit fails when an `AddUIObj` source registration site changes without review, even if it reuses an existing identifier.
- The audit fails when any generated or custom `PacketFunctions*.h` adds or removes a `Send*` entrypoint without updating the exact baseline.
- The audit also baselines non-`PacketFunctions` `Send*` helper definitions, including old combat/item wrappers and UI dispatch helpers, so bypass paths require review.
- `PointerFallback` is intentionally not equivalent to native focus navigation. Runtime validation remains required for drag/hold/release, double-click, text composition and server-confirmed haptics.

