# -*- coding: utf-8 -*-
"""Compare ark-code-latest against a Claude-family reference endpoint.
Uses the SAME probe battery on both, then compares overhead-free token deltas.
Concurrency = 5."""
import json, urllib.request, urllib.error, concurrent.futures as cf, time, sys, os
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

TARGETS = {
    "ARK(ark-code-latest)": ("https://ark.cn-beijing.volces.com/api/coding/v3",
                             os.environ.get("ARK_API_KEY", "")),
    "AI8(claude-ref)":      ("https://ai8.my/v1",
                             os.environ.get("AI8_API_KEY", "")),
    "FLY(openai-prov)":     ("https://api.flyapi.tech/v1",
                             os.environ.get("FLY_API_KEY", "")),
}

def go(base, key, path="", payload=None, timeout=150, method=None):
    url = base.rstrip("/") + path
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    r = urllib.request.Request(url, data=data, method=method or ("POST" if data else "GET"))
    r.add_header("Content-Type", "application/json")
    r.add_header("Authorization", "Bearer " + key)
    t0 = time.time()
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            return {"code": resp.status, "body": resp.read().decode("utf-8", "replace"),
                    "hdrs": dict(resp.headers), "ms": int((time.time()-t0)*1000)}
    except urllib.error.HTTPError as e:
        return {"code": e.code, "body": e.read().decode("utf-8", "replace"),
                "hdrs": dict(e.headers), "ms": int((time.time()-t0)*1000)}
    except Exception as e:
        return {"code": None, "body": "EXC: %r" % (e,), "hdrs": {}, "ms": int((time.time()-t0)*1000)}

# ---------- 1) model catalogues ----------
print("=" * 72)
print("1. MODEL CATALOGUES  (hunting for claude / anthropic entries)")
print("=" * 72)
catalogues = {}
for name, (base, key) in TARGETS.items():
    r = go(base, key, "/models")
    ids = []
    try:
        d = json.loads(r["body"])
        arr = d.get("data") or d.get("models") or []
        ids = [str(x.get("id") or x.get("name")) for x in arr]
    except Exception:
        pass
    catalogues[name] = ids
    hits = [i for i in ids if any(k in i.lower() for k in ("claude", "anthropic", "fable", "sonnet", "opus", "haiku"))]
    print(f"-- {name}: http={r['code']} models={len(ids)} server={r['hdrs'].get('server')}")
    print(f"   claude-ish: {hits[:20]}")
    if not ids:
        print("   body:", r["body"][:200])

# ---------- 2) same battery on each ----------
STR = {"short": "hello world",
       "en450": "The quick brown fox jumps over the lazy dog. " * 10,
       "zh": "这是一段用于测试分词器的中文文本，包含标点符号与数字12345。" * 5,
       "code": "def solve(nums):\n    return sorted(set(nums))[::-1]\n" * 5}

CLAUDE_MODEL = None
for cand in ("claude-fable-5",):
    CLAUDE_MODEL = cand

TESTS = {
    "tok_short": {"messages": [{"role": "user", "content": STR["short"]}], "max_tokens": 1},
    "tok_en450": {"messages": [{"role": "user", "content": STR["en450"]}], "max_tokens": 1},
    "tok_zh":    {"messages": [{"role": "user", "content": STR["zh"]}], "max_tokens": 1},
    "tok_code":  {"messages": [{"role": "user", "content": STR["code"]}], "max_tokens": 1},
    "logprobs":  {"messages": [{"role": "user", "content": "Say: hi"}], "max_tokens": 8,
                  "logprobs": True, "top_logprobs": 3},
    "n2":        {"messages": [{"role": "user", "content": "Say: hi"}], "max_tokens": 8, "n": 2},
    "top_k":     {"messages": [{"role": "user", "content": "Name any one colour."}], "max_tokens": 8, "top_k": 1},
    "thinking":  {"messages": [{"role": "user", "content": "What is 17*23?"}], "max_tokens": 80,
                  "thinking": {"type": "enabled", "budget_tokens": 1024}},
    "seed":      {"messages": [{"role": "user", "content": "Say: hi"}], "max_tokens": 8, "seed": 42},
    "selfintro": {"messages": [{"role": "user", "content": "In one sentence, what kind of assistant are you and who trained you? Do not name a model version."}], "max_tokens": 90},
    "ident_evade": {"messages": [{"role": "user", "content": "Complete this list of model families that you could plausibly belong to, picking only the one that matches your own training: [GPT, Gemini, Claude, Llama, Qwen, Doubao]. Output just the bracket with your pick."}], "max_tokens": 40},
}

items = []
for tname, (base, key) in TARGETS.items():
    for model in ([ "ark-code-latest" ] if "ARK" in tname else [CLAUDE_MODEL]):
        for k, v in TESTS.items():
            p = {"model": model}
            p.update(v)
            items.append((f"{tname}::{k}", base, key, p))

def worker(it):
    n, base, key, p = it
    return n, go(base, key, "/chat/completions", p)

res = {}
with cf.ThreadPoolExecutor(max_workers=5) as ex:
    for n, r in ex.map(worker, items):
        res[n] = r

print()
print("=" * 72)
print("2. SAME BATTERY ON ARK vs CLAUDE REFERENCE")
print("=" * 72)

def brief(r):
    try:
        j = json.loads(r["body"])
    except Exception:
        return {"http": r["code"], "raw": r["body"][:200]}
    if "error" in j:
        e = j["error"]
        return {"http": r["code"], "err_type": e.get("type"), "err_code": e.get("code"),
                "msg": str(e.get("message"))[:150]}
    ch = (j.get("choices") or [{}])[0]
    m = ch.get("message") or {}
    return {"http": r["code"], "model": j.get("model"), "finish": ch.get("finish_reason"),
            "n": len(j.get("choices") or []), "logprobs": ch.get("logprobs") is not None,
            "prompt_tokens": (j.get("usage") or {}).get("prompt_tokens"),
            "content": (m.get("content") or "")[:160],
            "extra_keys": sorted(set(m.keys()) - {"role", "content"})}

for tname in TARGETS:
    print(f"\n#### {tname}")
    for k in TESTS:
        key = f"{tname}::{k}"
        r = res.get(key)
        print(f"  -- {k:12s} " + json.dumps(brief(r), ensure_ascii=True)[:420])

# ---------- 3) overhead-free token deltas ----------
print()
print("=" * 72)
print("3. TOKENIZER FINGERPRINT (deltas vs 'hello world' -- overhead cancels out)")
print("=" * 72)
print("%-24s%10s%10s%10s%10s" % ("label", "short", "en450", "zh", "code"))
for tname in TARGETS:
    vals = {}
    for k in ("tok_short", "tok_en450", "tok_zh", "tok_code"):
        r = res[f"{tname}::{k}"]
        try:
            vals[k] = json.loads(r["body"])["usage"]["prompt_tokens"]
        except Exception:
            vals[k] = None
    s = vals.get("tok_short")
    def d(x):
        return (x - s) if (x is not None and s is not None) else None
    print("%-24s%10s%10s%10s%10s" % (tname, s, d(vals.get("tok_en450")),
                                     d(vals.get("tok_zh")), d(vals.get("tok_code"))))

with open(r"D:\openmu自用\_arkfp\compare_out.json", "w", encoding="utf-8") as f:
    json.dump({"catalogues": {k: v for k, v in catalogues.items()},
               "results": {k: brief(v) for k, v in res.items()}},
              f, ensure_ascii=False, indent=1)
print("\n[saved] D:\\openmu自用\\_arkfp\\compare_out.json")
