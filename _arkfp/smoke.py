# -*- coding: utf-8 -*-
"""Smoke probe: endpoint metadata + minimal chat call, raw dump."""
import json, urllib.request, urllib.error, sys, os

BASE = "https://ark.cn-beijing.volces.com/api/coding/v3"
KEY = os.environ.get("ARK_API_KEY", "")
MODEL = "ark-code-latest"

def req(url, method="GET", payload=None, timeout=60):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    r = urllib.request.Request(url, data=data, method=method)
    r.add_header("Content-Type", "application/json")
    r.add_header("Authorization", "Bearer " + KEY)
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            body = resp.read().decode("utf-8", "replace")
            return {"status": resp.status, "headers": dict(resp.headers), "body": body}
    except urllib.error.HTTPError as e:
        return {"status": e.code, "headers": dict(e.headers), "body": e.read().decode("utf-8", "replace")}
    except Exception as e:
        return {"status": None, "headers": {}, "body": "EXC: %r" % (e,)}

out = {}

# 1) model list
out["GET /models"] = req(BASE + "/models")

# 2) minimal chat
out["POST /chat/completions (minimal)"] = req(BASE + "/chat/completions", "POST", {
    "model": MODEL,
    "messages": [{"role": "user", "content": "Reply with exactly: OK"}],
    "max_tokens": 16,
})

# 3) anthropic-style path probe (does it exist?)
out["POST /v1/messages (anthropic path)"] = req(
    "https://ark.cn-beijing.volces.com/api/coding/v1/messages", "POST",
    {"model": MODEL, "max_tokens": 16, "messages": [{"role": "user", "content": "hi"}]})

print(json.dumps(out, ensure_ascii=False, indent=1)[:12000])
