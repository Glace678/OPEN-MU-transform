# -*- coding: utf-8 -*-
"""Find a LIVE Claude-family channel on the user's own relays, for a fair A/B."""
import json, urllib.request, urllib.error, concurrent.futures as cf, sys, os
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

AI8 = "https://ai8.my"
K_OA = os.environ.get("AI8_API_KEY", "")
K_AN = os.environ.get("ANTHROPIC_AUTH_TOKEN", "")

def go(url, key, payload=None, timeout=90):
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    r = urllib.request.Request(url, data=data, method="POST" if data else "GET")
    r.add_header("Content-Type", "application/json")
    r.add_header("Authorization", "Bearer " + key)
    r.add_header("x-api-key", key)
    r.add_header("anthropic-version", "2023-06-01")
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            return resp.status, resp.read().decode("utf-8", "replace")[:400]
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")[:400]
    except Exception as e:
        return None, "EXC %r" % (e,)

tasks = []
for label, key in (("oa", K_OA), ("an", K_AN)):
    tasks.append((f"{label}::GET /v1/models", AI8 + "/v1/models", key, None))
    tasks.append((f"{label}::GET /api/models", AI8 + "/api/models", key, None))

CAND = ["claude-fable-5", "claude-sonnet-4-5", "claude-sonnet-4-5-20250929",
        "claude-opus-4-1", "claude-3-7-sonnet-latest", "claude-3-5-sonnet-20241022"]
for c in CAND:
    tasks.append((f"oa::chat::{c}", AI8 + "/v1/chat/completions", K_OA,
                  {"model": c, "max_tokens": 8, "messages": [{"role": "user", "content": "hi"}]}))
tasks.append(("an::messages::claude-fable-5", AI8 + "/v1/messages", K_AN,
              {"model": "claude-fable-5", "max_tokens": 16,
               "messages": [{"role": "user", "content": "Reply with exactly: OK"}]}))
tasks.append(("an::messages::claude-sonnet-4-5", AI8 + "/v1/messages", K_AN,
              {"model": "claude-sonnet-4-5", "max_tokens": 16,
               "messages": [{"role": "user", "content": "Reply with exactly: OK"}]}))

with cf.ThreadPoolExecutor(max_workers=5) as ex:
    futs = {ex.submit(go, u, k, p): n for n, u, k, p in tasks}
    out = {}
    for f in cf.as_completed(futs):
        out[futs[f]] = f.result()

for n, _, _, _ in tasks:
    st, body = out[n]
    print("-- %-40s http=%s  %s" % (n, st, body.replace("\n", " ")[:220]))
