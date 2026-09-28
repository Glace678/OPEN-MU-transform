import os, json, urllib.request, urllib.error

tok = os.environ["GH_TOKEN"]


def call(url, data=None):
    headers = {
        "Authorization": f"Bearer {tok}",
        "Accept": "application/json",
        "X-GitHub-Api-Version": "2026-03-10",
    }
    body = None
    if data is not None:
        body = json.dumps(data).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=45) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except Exception as e:
        return None, repr(e)


def show_models(tag, url):
    s, b = call(url)
    print(f"--- {tag} list: HTTP {s} ---")
    try:
        j = json.loads(b)
        arr = j if isinstance(j, list) else j.get("data", j.get("models", []))
        ids = sorted(m.get("id", "?") for m in arr)
        print(f"count={len(ids)}")
        print(", ".join(ids))
    except Exception:
        print(b[:800])
    print()


def show_chat(tag, url, model):
    payload = {
        "model": model,
        "messages": [{"role": "user", "content": "Reply with the single word OK"}],
        "max_tokens": 10,
    }
    s, b = call(url, payload)
    print(f"--- {tag} chat ({model}): HTTP {s} ---")
    try:
        j = json.loads(b)
        print("reply:", j["choices"][0]["message"]["content"])
        if "usage" in j:
            print("usage:", j["usage"])
    except Exception:
        print(b[:800])
    print()


show_models("AZURE", "https://models.inference.ai.azure.com/models")
show_chat("AZURE", "https://models.inference.ai.azure.com/chat/completions", "gpt-4.1")
show_models("GITHUB", "https://models.github.ai/models")
show_chat("GITHUB", "https://models.github.ai/inference/chat/completions", "openai/gpt-4.1")
