# -*- coding: utf-8 -*-
import os as _os, sys as _sys
import config
import json, hashlib, os, sys

MANIFEST = os.path.join(config.PUBLISH, 'Server', 'manifest.json')  # not used; patch the live one
LIVE = config.SERVER.manifest
ROOT = config.SERVER()
TARGETS = ['App/Game/Main.exe', 'App/Game/MUnique.Client.Library.dll']

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
    return p.replace('\\', '/').lstrip('./')

updated = []
for e in files:
    p = norm(get_path(e))
    if p in TARGETS:
        full = os.path.join(ROOT, *p.split('/'))
        data = open(full, 'rb').read()
        size = len(data)
        sha = hashlib.sha256(data).hexdigest()
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

with open(LIVE, 'w', encoding='utf-8') as f:
    json.dump(m, f, ensure_ascii=False, indent=2)

print('patched OK, entries changed:', len(updated))
for u in updated:
    print(' ', u[0])
    print('   old sha:', u[3])
    print('   new sha:', u[4])
