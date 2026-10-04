import os, json, urllib.request, urllib.error
from urllib.parse import urlparse

github_token = os.environ["GH_TOKEN"]
azure_token = os.environ.get("AZURE_MODELS_TOKEN", "")

# GitHub dates its REST API; a date that is too new is rejected with 415/410
# instead of silently ignored, so surface that instead of hiding it in a header.
API_VERSION = os.environ.get("GH_API_VERSION", "2026-03-10")


def call(url, data=None):
    host = urlparse(url).hostname or ""
    headers = {"Accept": "application/json"}
    if host.endswith(".github.ai"):
        headers["Authorization"] = f"Bearer {github_token}"
        headers["X-GitHub-Api-Version"] = API_VERSION
    elif host.endswith(".azure.com"):
        # Never send the GitHub token to the Azure inference endpoint; it needs a
        # separate credential (leave anonymous when none is configured).
        if azure_token:
            headers["Authorization"] = f"Bearer {azure_token}"
    body = None
    if data is not None:
        body = json.dumps(data).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=45) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")
        if e.code in (400, 410, 415) and "api-version" in body.lower():
            print(f"hint: X-GitHub-Api-Version {API_VERSION} was rejected; "
                  f"set GH_API_VERSION to a supported date", flush=True)
        return e.code, body
    except Exception as e:
        return None, repr(e)


failures = []


def show_models(tag, url):
    s, b = call(url)
    print(f"--- {tag} list: HTTP {s} ---")
    if s != 200:
        failures.append(f"{tag} models returned HTTP {s}")
    try:
        j = json.loads(b)
        arr = j if isinstance(j, list) else j.get("data", j.get("models", []))
        ids = sorted(m.get("id", "?") for m in arr)
        print(f"count={len(ids)}")
        print(", ".join(ids))
    except Exception as parse_error:
        failures.append(f"{tag} models: unparseable response ({parse_error})")
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
    if s != 200:
        failures.append(f"{tag} chat returned HTTP {s}")
    try:
        j = json.loads(b)
        print("reply:", j["choices"][0]["message"]["content"])
        if "usage" in j:
            print("usage:", j["usage"])
    except Exception as parse_error:
        failures.append(f"{tag} chat: unparseable response ({parse_error})")
        print(b[:800])
    print()


show_models("AZURE", "https://models.inference.ai.azure.com/models")
show_chat("AZURE", "https://models.inference.ai.azure.com/chat/completions", "gpt-4.1")
show_models("GITHUB", "https://models.github.ai/models")
show_chat("GITHUB", "https://models.github.ai/inference/chat/completions", "openai/gpt-4.1")

if failures:
    print("FAIL:", "; ".join(failures))
    raise SystemExit(1)
