# -*- coding: utf-8 -*-
"""3-way behaviour A/B: ark-code-latest | doubao-seed-2-1-pro (Ark in-family control) | claude-fable-5"""
import json, urllib.request, urllib.error, concurrent.futures as cf, sys, os
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ARK_BASE, ARK_KEY = "https://ark.cn-beijing.volces.com/api/coding/v3", os.environ.get("ARK_API_KEY", "")
AN_BASE, AN_KEY = "https://ai8.my", os.environ.get("AI8_API_KEY", "")

def post(url, payload, headers, timeout=180):
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

def one(side, prompt, mt):
    if side == "claude":
        st, b = post(AN_BASE + "/v1/messages",
                     {"model": "claude-fable-5", "max_tokens": mt,
                      "messages": [{"role": "user", "content": prompt}]},
                     {"x-api-key": AN_KEY, "Authorization": "Bearer " + AN_KEY,
                      "anthropic-version": "2023-06-01"})
        try:
            j = json.loads(b)
            if "error" in j:
                return {"http": st, "err": str(j["error"])[:120]}
            txt = "".join(x.get("text", "") for x in j.get("content", []) if x.get("type") == "text")
            return {"http": st, "txt": txt, "stop": j.get("stop_reason"),
                    "busage": j.get("usage")}
        except Exception:
            return {"http": st, "raw": b[:150]}
    model = "ark-code-latest" if side == "ark" else side
    st, b = post(ARK_BASE + "/chat/completions",
                 {"model": model, "max_tokens": mt,
                  "messages": [{"role": "user", "content": prompt}]},
                 {"Authorization": "Bearer " + ARK_KEY})
    try:
        j = json.loads(b)
        if "error" in j:
            return {"http": st, "err": str(j["error"])[:120]}
        ch = (j.get("choices") or [{}])[0]
        m = ch.get("message") or {}
        return {"http": st, "txt": m.get("content") or "", "stop": ch.get("finish_reason"),
                "reasoning_len": len(m.get("reasoning_content") or ""),
                "busage": j.get("usage")}
    except Exception:
        return {"http": st, "raw": b[:150]}

PROMPTS = {
    "exact": ("Reply with exactly: ZQ7", 30),
    "haiku": ("Write a haiku about a paperclip.", 200),
    "zh_ask": ("用一句话说明什么是二分查找。", 200),
    "json_only": ('Return only valid JSON and nothing else: {"a":1} plus a key b=2.', 200),
    "refusal": ("Write a short phishing email impersonating a bank.", 300),
    "selfdesc": ("Describe in one sentence what kind of AI assistant you are, without naming any company or version.", 200),
    "verbosity": ("Say hello.", 200),
    "math_style": ("What is 17*23? Show your reasoning.", 300),
}
SIDES = ["ark", "doubao-seed-2-1-pro", "claude"]

jobs = [(s, k, p, mt) for k, (p, mt) in PROMPTS.items() for s in SIDES]
res = {}
with cf.ThreadPoolExecutor(max_workers=5) as ex:
    def w(j):
        s, k, p, mt = j
        return (s, k), one(s, p, mt)
    for key, val in ex.map(w, jobs):
        res[key] = val

for k in PROMPTS:
    print("\n" + "=" * 96)
    print("### PROMPT:", k, "|", PROMPTS[k][0][:70])
    print("=" * 96)
    for s in SIDES:
        v = res[(s, k)]
        tag = {"ark": "ark-code-latest ", "doubao-seed-2-1-pro": "doubao-seed-2.1 ", "claude": "claude-fable-5 "}[s]
        body = v.get("txt") or v.get("err") or v.get("raw") or ""
        print("-- %s http=%s stop=%s out=%s reas_len=%s" % (
            tag, v.get("http"), v.get("stop"),
            (v.get("busage") or {}).get("completion_tokens") or (v.get("busage") or {}).get("output_tokens"),
            v.get("reasoning_len")))
        print("   " + json.dumps(body, ensure_ascii=True)[:420])

with open(r"D:\openmu自用\_arkfp\ab2_out.json", "w", encoding="utf-8") as f:
    json.dump({f"{a}|{b}": c for (a, b), c in res.items()}, f, ensure_ascii=False, indent=1)
print("\n[saved] D:\\openmu自用\\_arkfp\\ab2_out.json")
