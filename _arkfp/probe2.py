# -*- coding: utf-8 -*-
"""Probe 2: filter model list + full raw chat response."""
import json, urllib.request, urllib.error, os

BASE = "https://ark.cn-beijing.volces.com/api/coding/v3"
KEY = os.environ.get("ARK_API_KEY", "")
MODEL = "ark-code-latest"

def req(url, method="GET", payload=None, timeout=120):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    r = urllib.request.Request(url, data=data, method=method)
    r.add_header("Content-Type", "application/json")
    r.add_header("Authorization", "Bearer " + KEY)
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            return resp.status, dict(resp.headers), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, dict(e.headers), e.read().decode("utf-8", "replace")
    except Exception as e:
        return None, {}, "EXC: %r" % (e,)

st, hd, body = req(BASE + "/models")
print("== /models status:", st)
try:
    models = json.loads(body)["data"]
    print("== total models:", len(models))
    for m in models:
        mid = m.get("id", "")
        if any(k in mid.lower() for k in ("ark", "code", "claude", "anthropic", "seed")):
            print("   -", mid, "|", m.get("name"), "|", m.get("status"), "|", m.get("version"))
except Exception as e:
    print("parse fail:", e, body[:500])

print()
print("== chat/completions (ark-code-latest) ==")
st, hd, body = req(BASE + "/chat/completions", "POST", {
    "model": MODEL,
    "messages": [{"role": "user", "content": "Reply with exactly one word: OK"}],
    "max_tokens": 16,
})
print("status:", st)
print("headers:", json.dumps({k: v for k, v in hd.items()}, ensure_ascii=False))
print("body:", body)
