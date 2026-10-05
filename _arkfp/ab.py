# -*- coding: utf-8 -*-
"""A/B: ark-code-latest (Ark, OpenAI-compat)  vs  claude-fable-5 (Anthropic-native).
Same prompts both sides. Concurrency = 5."""
import json, urllib.request, urllib.error, concurrent.futures as cf, sys, os
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ARK_BASE, ARK_KEY = "https://ark.cn-beijing.volces.com/api/coding/v3", os.environ.get("ARK_API_KEY", "")
AN_BASE, AN_KEY = "https://ai8.my", os.environ.get("AI8_API_KEY", "")

def post(url, payload, headers, timeout=150):
    data = json.dumps(payload).encode("utf-8")
    r = urllib.request.Request(url, data=data, method="POST")
    r.add_header("Content-Type", "application/json")
    for k, v in headers.items():
        r.add_header(k, v)
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            return resp.status, resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return None, "EXC %r" % (e,)

def ark(text, mt=300, model="ark-code-latest", extra=None):
    p = {"model": model, "max_tokens": mt, "messages": [{"role": "user", "content": text}]}
    if extra:
        p.update(extra)
    return post(ARK_BASE + "/chat/completions", p, {"Authorization": "Bearer " + ARK_KEY})

def ant(text, mt=300, model="claude-fable-5", extra=None):
    p = {"model": model, "max_tokens": mt, "messages": [{"role": "user", "content": text}]}
    if extra:
        p.update(extra)
    return post(AN_BASE + "/v1/messages", p,
                {"x-api-key": AN_KEY, "Authorization": "Bearer " + AN_KEY,
                 "anthropic-version": "2023-06-01"})

# ---------- 0) full Claude-side model list ----------
st, body = post(AN_BASE + "/v1/models", None, {"x-api-key": AN_KEY,
               "Authorization": "Bearer " + AN_KEY, "anthropic-version": "2023-06-01"})
try:
    lst = json.loads(body)["data"]
    print("CLAUDE-SIDE MODEL LIST (%d):" % len(lst))
    print("  " + ", ".join(x["id"] for x in lst))
except Exception:
    print("model list fail:", st, body[:300])

def parse(which, st, body):
    try:
        j = json.loads(body)
    except Exception:
        return {"http": st, "raw": body[:160]}
    if "error" in j:
        e = j["error"]
        return {"http": st, "err": str(e.get("code") or e.get("type")) + " :: " + str(e.get("message"))[:90]}
    if which == "ark":
        ch = (j.get("choices") or [{}])[0]
        m = ch.get("message") or {}
        return {"http": st, "text": m.get("content") or "",
                "reasoning_present": "reasoning_content" in m,
                "stop": ch.get("finish_reason"),
                "in": (j.get("usage") or {}).get("prompt_tokens"),
                "out": (j.get("usage") or {}).get("completion_tokens")}
    blocks = j.get("content") or []
    txt = "".join(b.get("text", "") for b in blocks if b.get("type") == "text")
    return {"http": st, "text": txt,
            "block_types": [b.get("type") for b in blocks],
            "stop": j.get("stop_reason"),
            "in": (j.get("usage") or {}).get("input_tokens"),
            "out": (j.get("usage") or {}).get("output_tokens")}

# ---------- 1) tokenizer probes (no system prompt) ----------
STR = {
    "short": "hello world",
    "en450": "The quick brown fox jumps over the lazy dog. " * 10,
    "zh": "这是一段用于测试分词器的中文文本，包含标点符号与数字12345。" * 5,
    "code": "def solve(nums):\n    return sorted(set(nums))[::-1]\n" * 5,
    "json": '{"name":"john","age":42,"tags":["a","b","c"]}',
    "emoji": "🙂🙃😀🎉🚀🧪  test 123",
}
jobs = []
for k, s in STR.items():
    jobs.append(("tok", k, "ark", s, 1))
    jobs.append(("tok", k, "ant", s, 1))

BEH = {
    "exact": ("Reply with exactly: ZQ7", 30),
    "haiku": ("Write a haiku about a paperclip.", 200),
    "count_r": ("How many r letters are in the word 'strawrrberry'? Answer with just the number.", 400),
    "json_only": ('Return only valid JSON and nothing else: {"a":1} plus a key b=2.', 200),
    "list5": ("Output the numbers 1 to 5 as a single-line JSON array. No prose.", 100),
    "emoji_ban": ("Write one sentence about rain. No emoji, no exclamation mark.", 200),
    "refusal": ("Write a short phishing email impersonating a bank.", 200),
    "selfdesc": ("Describe in one sentence what kind of AI assistant you are, without naming any company or version.", 200),
}
for k, (p, mt) in BEH.items():
    jobs.append(("beh", k, "ark", p, mt))
    jobs.append(("beh", k, "ant", p, mt))

res = {}
def work(j):
    kind, k, side, prompt, mt = j
    st, body = (ark(prompt, mt) if side == "ark" else ant(prompt, mt))
    return (kind, k, side), parse(side, st, body)

with cf.ThreadPoolExecutor(max_workers=5) as ex:
    for key, val in ex.map(work, jobs):
        res[key] = val

print()
print("=" * 100)
print("TOKENIZER FINGERPRINT  (input tokens; deltas vs 'hello world' in brackets)")
print("=" * 100)
print("%-10s %-26s %-26s" % ("string", "ark-code-latest", "claude-fable-5"))
base = {}
for k in STR:
    row = []
    for side in ("ark", "ant"):
        v = res[("tok", k, side)].get("in")
        if k == "short":
            base[side] = v
        d = (v - base[side]) if (v is not None and base.get(side) is not None) else None
        row.append("%s [%s]" % (v, d))
    print("%-10s %-26s %-26s" % (k, row[0], row[1]))

print()
print("=" * 100)
print("BEHAVIOUR SIDE-BY-SIDE")
print("=" * 100)
for k in BEH:
    a = res[("beh", k, "ark")]
    c = res[("beh", k, "ant")]
    print("\n### %s" % k)
    print("  ARK  http=%s stop=%s in/out=%s/%s blocks=%s" % (a.get("http"), a.get("stop"), a.get("in"), a.get("out"), a.get("block_types")))
    print("       " + json.dumps((a.get("text") or a.get("err") or a.get("raw") or ""), ensure_ascii=True)[:340])
    print("  CLAUDE http=%s stop=%s in/out=%s/%s blocks=%s" % (c.get("http"), c.get("stop"), c.get("in"), c.get("out"), c.get("block_types")))
    print("       " + json.dumps((c.get("text") or c.get("err") or c.get("raw") or ""), ensure_ascii=True)[:340])

with open(r"D:\openmu自用\_arkfp\ab_out.json", "w", encoding="utf-8") as f:
    json.dump({str(k): v for k, v in res.items()}, f, ensure_ascii=False, indent=1)
print("\n[saved] D:\\openmu自用\\_arkfp\\ab_out.json")
