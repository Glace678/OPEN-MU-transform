# -*- coding: utf-8 -*-
import os as _os, sys as _sys
import config
import json, hashlib, os, shutil, sys

try:
    MANIFEST = os.path.join(config.PUBLISH, 'Server', 'manifest.json')  # not used; patch the live one
    LIVE = config.SERVER.manifest
    ROOT = config.SERVER()
except FileNotFoundError as exc:
    sys.exit(str(exc))
TARGETS = ['App/Game/Main.exe', 'App/Game/MUnique.Client.Library.dll']
_CHUNK = 1024 * 1024


def sha256_of(path):
    """Stream the file so multi-GB Main.exe never sits in memory at once."""
    digest = hashlib.sha256()
    with open(path, 'rb') as handle:
        for block in iter(lambda: handle.read(_CHUNK), b''):
            digest.update(block)
    return digest.hexdigest()

with open(LIVE, encoding='utf-8') as f:
    m = json.load(f)

files = m['files'] if isinstance(m, dict) and 'files' in m else m
print('entries:', len(files))

# discover key names from first entry
sample = files[0]
print('sample keys:', list(sample.keys()))

def get_path(e):
    for k in ('path', 'Path', 'relativePath', 'RelativePath', 'name', 'Name'):
        if k in e:
            return e[k]
    raise KeyError('no path key: ' + str(e))

def norm(p):
    p = p.replace('\\', '/')
    return p[2:] if p.startswith('./') else p

updated = []
for e in files:
    p = norm(get_path(e))
    if p in TARGETS:
        full = os.path.join(ROOT, *p.split('/'))
        if not os.path.isfile(full):
            print('missing:', full, file=sys.stderr)
            continue
        size = os.path.getsize(full)
        sha = sha256_of(full)
        old_size = e.get('size', e.get('Size'))
        old_sha = e.get('sha256', e.get('Sha256', e.get('hash', e.get('Hash'))))
        # set the keys present in the schema
        for k in list(e.keys()):
            lk = k.lower()
            if lk == 'size':
                e[k] = size
            elif lk in ('sha256', 'hash'):
                e[k] = sha
        updated.append((p, old_size, size, old_sha, sha))
        print('updated:', p, old_size, '->', size)

updated_paths = {u[0] for u in updated}
missing_targets = [target for target in TARGETS if target not in updated_paths]
if missing_targets:
    # Refuse to rewrite the live manifest on a partial patch: otherwise the
    # missing target keeps a stale size/sha256 while we still report success.
    print('ERROR: required target(s) not patched:', ', '.join(missing_targets), file=sys.stderr)
    sys.exit(1)

# Keep a copy of the previous manifest next to the live one before rewriting.
shutil.copy2(LIVE, LIVE + '.bak')
with open(LIVE, 'w', encoding='utf-8') as f:
    json.dump(m, f, ensure_ascii=False, indent=2)

print('patched OK, entries changed:', len(updated))
for u in updated:
    print(' ', u[0])
    print('   old sha:', u[3])
    print('   new sha:', u[4])
