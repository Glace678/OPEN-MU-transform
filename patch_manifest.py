# -*- coding: utf-8 -*-
"""Streamingly calculates SHA256 hashes and syncs server manifest entries.

Avoids loading large binary executables into memory by streaming in 1MB chunks.
Ensures manifest updates are atomic by validating all target binaries before writing.
"""

import hashlib
import json
import os
import shutil
import subprocess
import sys

import config

try:
    MANIFEST = os.path.join(config.PUBLISH, 'Server', 'manifest.json')  # not used; patch the live one
    LIVE = config.SERVER.manifest
    ROOT = config.SERVER()
except FileNotFoundError as exc:
    sys.exit(str(exc))

TARGETS = ['App/Game/Main.exe', 'App/Game/MUnique.Client.Library.dll']
_CHUNK = 1024 * 1024
SIGNATURE = LIVE + '.sig'
SIGNER_SCRIPT = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), 'OpenMU', 'tools', 'ManifestSigner', 'Sign-Manifest.ps1')
SIGNING_KEY_ENV = 'OPENMU_MANIFEST_SIGNING_KEY'


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
if not isinstance(files, list) or not files:
    sys.exit('ERROR: manifest has no file entries')
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
    while p.startswith('./'):
        p = p[2:]
    return p.lstrip('/')

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

# A signed deployment must stay signed. The launcher verifies the detached signature over the
# raw manifest bytes before it trusts any size/sha256 value and fails closed on a mismatch, so
# rewriting the live manifest without re-signing would break every later start. Check the key and
# the signer up front, while the live manifest is still untouched.
requires_signature = os.path.isfile(SIGNATURE)
signer_host = None
if requires_signature:
    if not os.environ.get(SIGNING_KEY_ENV):
        print(f'ERROR: {os.path.basename(SIGNATURE)} exists, so the patched manifest must be re-signed, '
              f'but {SIGNING_KEY_ENV} is not set. The live manifest was not modified.', file=sys.stderr)
        sys.exit(1)
    if not os.path.isfile(SIGNER_SCRIPT):
        print(f'ERROR: manifest signer not found: {SIGNER_SCRIPT}', file=sys.stderr)
        sys.exit(1)
    signer_host = shutil.which('pwsh') or shutil.which('powershell')
    if not signer_host:
        print('ERROR: neither pwsh nor powershell is available to re-sign the patched manifest.',
              file=sys.stderr)
        sys.exit(1)

# Keep a copy of the previous manifest (and signature) next to the live one before rewriting atomically.
shutil.copy2(LIVE, LIVE + '.bak')
if requires_signature:
    shutil.copy2(SIGNATURE, SIGNATURE + '.bak')

tmp_live = LIVE + '.tmp'
tmp_signature = None
try:
    with open(tmp_live, 'w', encoding='utf-8') as f:
        json.dump(m, f, ensure_ascii=False, indent=2)
    if requires_signature:
        # Sign the exact bytes which are about to replace the live manifest; the rename keeps them unchanged.
        tmp_signature = tmp_live + '.sig'
        if os.path.exists(tmp_signature):
            os.remove(tmp_signature)
        subprocess.run(
            [signer_host, '-NoProfile', '-File', SIGNER_SCRIPT,
             '-ManifestPath', tmp_live,
             '-PrivateKeyXmlPath', os.environ[SIGNING_KEY_ENV]],
            check=True)
        if not os.path.isfile(tmp_signature):
            raise RuntimeError('the signer did not produce a signature for the patched manifest')
    os.replace(tmp_live, LIVE)
    if tmp_signature is not None:
        os.replace(tmp_signature, SIGNATURE)
except Exception:
    for leftover in (tmp_live, tmp_live + '.sig'):
        if os.path.exists(leftover):
            try:
                os.remove(leftover)
            except OSError:
                pass
    raise

print('patched OK, entries changed:', len(updated))
for u in updated:
    print(' ', u[0])
    print('   old sha:', u[3])
    print('   new sha:', u[4])
