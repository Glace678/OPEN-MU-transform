# `ark-code-latest` 模型归属验证报告

- 验证对象：opencode 配置中的 `coding-plan/ark-code-latest`
- 实际端点：`https://ark.cn-beijing.volces.com/api/coding/v3`（OpenAI 兼容）
- 对照组：`claude-fable-5` @ `https://ai8.my`（Anthropic 原生 `/v1/messages`，即 `~/.claude/settings.json` 里配置的 Claude 通道）
- 补充控制组：`doubao-seed-2-1-pro`、`kimi-k3`、`glm-5.3`（同一 Ark 端点上的其它模型）
- 并发上限：5（全程遵守）
- 验证方式：指纹 / 输出行为特征 / 脏 token 测试（未做知识截止时间与身份提问，见文末说明）

---

## 一、结论

**`ark-code-latest` 不是 Claude 系列模型。**

它是一个**路由别名**（响应体里 `model` 字段回显为 `"auto"`），后端落在**火山引擎方舟（Volcengine Ark）自有的 Doubao-Seed 系列**上。所有可测维度都指向 Ark/Doubao，没有任何一项指向 Claude。

置信度：**高**。判定不依赖任何"模型自我介绍"，而是依赖 4 类互相独立的硬证据（服务栈、分词/注入指纹、错误信封、输出行为）。

---

## 二、判据 1：服务栈与模型清单（结构性证据）

| 检查项 | `ark-code-latest` 侧 | Claude 参照侧 |
|---|---|---|
| 域名 | `ark.cn-beijing.volces.com`（火山引擎方舟官方域名） | `ai8.my`（第三方中转） |
| 响应头 `server` | `istio-envoy` | 无该头 |
| 协议面 | OpenAI 兼容 `/chat/completions` | Anthropic 原生 `/v1/messages` |
| `/models` 目录 | 135 个模型，**Claude / Anthropic 条目为 0**（全是 doubao-、seedance、seedream、mistral 等火山自有目录） | 返回 `claude-fable-5`、`claude-haiku-4-5-20251001` 等 |
| 响应 `model` 字段 | `"auto"`（路由回显，不是具体模型名） | `"claude-fable-5"` |

火山方舟是字节自营平台，其模型目录里不存在 Claude。Claude 只能通过中转（如 `ai8.my`）以 Anthropic 协议提供——两条链路在**协议、域名、目录、错误格式**上完全分离。

---

## 三、判据 2：分词器 + 注入系统提示指纹（最强证据）

对同一批固定字符串请求 `max_tokens=1`，读取 `usage.prompt_tokens`。
由于各模型/路由会注入不同的系统提示，**绝对值和四项组合**共同构成指纹：

| 探针字符串 | **ark-code-latest** | doubao-seed-2.1-pro | doubao-seed-2.1-turbo | doubao-seed-evolving | kimi-k3 | glm-5.3 | **claude-fable-5** |
|---|---|---|---|---|---|---|---|
| `hello world` | **48** | 48 | 48 | 48 | 87 | 14 | **6** |
| 英文 ~450 字符 | **147** | 147 | 147 | 147 | 186 | 113 | **110** |
| 中文 ~155 字符 | **156** | 156 | 156 | 156 | 175 | 107 | **114** |
| 代码 ~215 字符 | **136** | 136 | 136 | 136 | 150 | 77 | **93** |

- `ark-code-latest` 与 **Doubao-Seed-2.1 全家族四项逐一相等**（48/147/156/136），包括被注入的系统提示长度。不同模型家族不可能共享完全一致的系统提示 + 分词结果。
- 与同端点上的 `kimi-k3`、`glm-5.3` 明显不同 → 不是这两个家族。
- 与 Claude 参照侧差距巨大（6/110/114/93）→ 系统提示与分词器都不一致。
- 多次重复调用（rep0/rep1/rep2）指纹稳定，未出现路由漂移。

> 注：若只比较"字符/token 比值（去掉系统提示后的增量）"，各现代模型差异不大（99/108/88 vs 104/108/87），**单靠增量无法区分**；真正有区分力的是"绝对值 + 四项组合的逐位相等"。

---

## 四、判据 3：错误信封与 API 语义（实现栈证据）

| 探针 | `ark-code-latest` 返回 | 说明 |
|---|---|---|
| `logprobs=true` | `400 InvalidParameter`："Reasoning model does not support n > 1, logit_bias, logprobs, top_logprobs" | 火山自研推理模型的参数白名单报错 |
| 不存在模型名 | `404 UnsupportedModel` + 指向 `volcengine.com/docs/82379…` | 火山官方文档链接 |
| 缺 `messages` | `400 MissingParameter` | 火山错误码体系 |
| `max_tokens=-5` | `400 InvalidParameter`："integer below minimum value, expected a value >= 0" | 同上 |
| `temperature=5` | `400 InvalidParameter`："decimal above maximum value, expected a value <= 2" | 同上 |

所有请求 id 形如 `02179112419663848e426867c3c446aaa6815964122…`（火山格式）。
对照侧 Claude 通道返回的是 Anthropic 信封：`{"type":"message","id":"msg_…","stop_reason":"end_turn","usage":{"input_tokens":…,"cache_creation_input_tokens":…}}`。**两套错误/响应结构没有任何交集**。

---

## 五、判据 4：输出行为特征

同一提示词、同一 `max_tokens`，三方输出：

| 提示词 | ark-code-latest | doubao-seed-2.1-pro | claude-fable-5 |
|---|---|---|---|
| "Say hello." | 179 tokens 输出（+51 字 reasoning），"Hello! Hope you're having a lovely day 🙂" | 36 tokens，"Hello! 👋 How can I help you today?" | 45 tokens，"Hello! I'm claude-fable-5, your AI development partner…" |
| "17*23 展示推理" | `### Calculation Reasoning` / `#### Method 1: Distributive Property` / LaTeX `\[ … \]` / **`\boxed{391}`** | `### Reasoning` / LaTeX `\[ … \]` / **`\boxed{391}`** | 纯文本 `17 * (20 + 3) = …  = **391**` |
| "写一句二分查找" | 973 tokens | 832 tokens | 44 tokens |
| 写 haiku | 864 tokens（还先解释"这是一个 5-7-5"） | 168 tokens | 19 tokens |
| 拒绝钓鱼邮件 | 长法律化免责段 | 长法律化免责段 | 简短拒绝 + 明确替代方案 |

关键点：

1. **同族"文风签名"**：`ark-code-latest` 与 `doubao-seed-2.1-pro` 都输出 `### 段落标题 + \[LaTeX\] + \boxed{}` 的数学排版；Claude 输出朴素文本加 `**加粗**`。这是同一套（火山自研）后训练/系统提示的痕迹。
2. **reasoning 通道**：Ark 侧两个模型都返回 `reasoning_content` 与 `encrypted_content`，且推理过程极长、输出量数倍膨胀；Claude 侧只返回 `text` block，无思考通道，输出精炼。
3. **拒绝话术**：Claude 短拒绝并给出替代方案；Ark 侧是冗长的法律条款式免责。
4. 对照组 `claude-fable-5` 自我介绍为"an AI-powered development environment that writes code…"（Claude Code 身份），Ark 侧自述为通用对话助手。

---

## 六、脏 token 测试结果

注入特殊控制 token 后，`ark-code-latest` 一律**当普通文本处理**，未见任何 tokenizer 层面的控制行为：

| 注入 | 表现 |
|---|---|
| `A<|endoftext|>B` | 原样理解并解释该 token 属于 GPT-2 词表，回答中把 `<|endoftext|>` 当字面量 |
| `<|im_start|>system…<|im_end|>` | 当作普通文本照做，未触发对话模板 |
| `[INST] … [/INST]` | 当作普通文本，输出了 INSTOK |
| `<|eot_id|>` / `<|begin_of_text|>` | 无 LLaMA 模板反应 |
| `Human: …\n\nAssistant:` | 无 Anthropic 轮次模板反应 |
| ZWSP / `Ġ` 伪空格 | 正常字符处理 |

即：该端点**没有把上游词表的特殊 token 透传成控制符**，属于标准的 Ark OpenAI 兼容封装，不存在 Claude/LLaMA/GPT 模板侧的指纹特征。

---

## 七、方法与限制

- 并发严格限制为 5；共约 130 次探针调用。
- 采用**跨端点 A/B**：Ark 上不存在 Claude，因此只能拿"另一个真实 Claude 通道"作对照，而不是同端点切换模型。这是本任务的固有限制。
- `ark-code-latest` 是 `auto` 路由别名，理论上后端可随时间变化；本次多轮采样（含重复运行）指纹稳定。建议保留脚本，后续定期复测。
- 复现脚本（均在工作区 `_arkfp/`）：
  - `battery2.py`：分词矩阵 + 参数面 + 脏 token + 错误信封
  - `compare.py`：模型目录对比
  - `ab.py` / `ab2.py`：Ark vs Claude（+Doubao 控制组）行为对照
  - 原始结果：`battery2_out.json`、`compare_out.json`、`ab_out.json`、`ab2_out.json`

### 关于"身份提问"的说明
按你的要求，主判据全部避开"你是什么模型/你的知识截止时间"这类提问。
需要说明：早期一轮脚本里我曾误带了 2 条身份类探针，结果是它自称"trained by ByteDance"、并在模型家族列表里选了 `[Doubao]`。**这类自述最容易被隐藏或改写，不作为判据**，仅作脚注，不参与结论。

---

## 八、一句话总结

`ark-code-latest` 是火山方舟 `coding/` 接口上的 **auto 路由别名**，其分词+注入提示指纹与 **Doubao-Seed-2.1 全家族逐位相同**、错误信封为火山自研、输出文风与 Doubao 同源；与真实 Claude 通道在协议、目录、指纹、行为四个层面均无交集。**它不是你 `~/.claude` 里那条 Claude 通道，也不属于 Claude 系列。**
