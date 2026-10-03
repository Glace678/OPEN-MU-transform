# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
W = _os.path.dirname(_os.path.abspath(__file__))
"""Deploy built locale data trees + fresh Main.exe to the live game and dev tree,
and upsert the launcher manifest. Idempotent. Use --apply to actually write."""
import json, os, shutil, hashlib, sys, datetime

APPLY = '--apply' in sys.argv
BUILD = os.path.join(W,'build')
MAIN_EXE = config.MAIN_EXE
LIVE = config.SERVER.game
DEV  = config.DEV_TREE
LIVE_MANIFEST = config.SERVER.manifest
STAMP = datetime.datetime.now().strftime('%Y%m%d-%H%M%S')

# Locale data dirs are whatever the build actually produced; a newly built locale
# deploys without this list needing an update.
BUILT = [d for d in sorted(os.listdir(BUILD))
         if os.path.isdir(os.path.join(BUILD, d))]

def sha(p):
    h=hashlib.sha256()
    with open(p,'rb') as fh:
        for chunk in iter(lambda: fh.read(1<<20), b''):
            h.update(chunk)
    return h.hexdigest().upper()

def copy_tree(src, dst):
    """Replace dst with src without leaving the game tree incomplete.

    rmtree+copytree left a window in which the directory was missing (or half
    copied); staging beside the target and swapping keeps either the old or the
    new tree present at all times.
    """
    n = 0
    for root, _, files in os.walk(src):
        n += len(files)
    if not APPLY:
        return n

    staging = dst + '.new'
    if os.path.exists(staging):
        shutil.rmtree(staging)
    shutil.copytree(src, staging)

    replaced = dst + '.old'
    if os.path.exists(replaced):
        shutil.rmtree(replaced)
    if os.path.exists(dst):
        os.replace(dst, replaced)
    try:
        os.replace(staging, dst)
    except Exception:
        # Put the previous tree back rather than leaving the game without data.
        if os.path.exists(replaced) and not os.path.exists(dst):
            os.replace(replaced, dst)
        raise
    if os.path.exists(replaced):
        shutil.rmtree(replaced, ignore_errors=True)
    return n

plan=[]
if not BUILT:
    print('no built locale dirs under', BUILD)
for d in BUILT:
    src=os.path.join(BUILD,d)
    for target in (os.path.join(LIVE,'Data','Local',d), os.path.join(DEV,'Data','Local',d)):
        n=copy_tree(src,target)
        plan.append((src,target,n))
        print(('COPY ' if APPLY else 'PLAN ')+f'{d} -> {target} ({n} files)')

# Main.exe
if os.path.exists(MAIN_EXE):
    plan.append((MAIN_EXE, os.path.join(LIVE,'Main.exe'),1))
    print(('COPY ' if APPLY else 'PLAN ')+f'Main.exe -> live')

# ---- manifest upsert ----
m=json.load(open(LIVE_MANIFEST,encoding='utf-8'))
files=m['files'] if isinstance(m,dict) and 'files' in m else m
by_path={e['path']:e for e in files}

def upsert(rel, disk):
    size=os.path.getsize(disk); h=sha(disk)
    pref='App/Game/'
    path=pref+rel.replace('\\','/')
    e=by_path.get(path)
    if e is None:
        e={'path':path,'size':size,'sha256':h}; by_path[path]=e; files.append(e)
        act='ADD '
    else:
        e['size']=size; e['sha256']=h; act='UPD '
    print(('MAN '+act if APPLY else 'MAN-PLAN ')+path, size)

for d in BUILT:
    src=os.path.join(BUILD,d)
    for root,_,fs in os.walk(src):
        for fn in fs:
            full=os.path.join(root,fn)
            rel=os.path.relpath(full,BUILD).replace('\\','/')  # Ger/...
            upsert('Data/Local/'+rel, full)
# Main.exe manifest entry
if os.path.exists(MAIN_EXE):
    upsert('Main.exe', MAIN_EXE)

if APPLY:
    bak=LIVE_MANIFEST+f'.bak-{STAMP}'
    shutil.copy2(LIVE_MANIFEST,bak); print('manifest backup ->',bak)
    json.dump(m,open(LIVE_MANIFEST,'w',encoding='utf-8'),ensure_ascii=False,indent=2)
    # backup + copy Main.exe
    live_main=os.path.join(LIVE,'Main.exe')
    if os.path.exists(MAIN_EXE):
        b=live_main+f'.bak-{STAMP}'
        if os.path.exists(live_main) and not os.path.exists(b): shutil.copy2(live_main,b)
        shutil.copy2(MAIN_EXE,live_main)
    print('DEPLOY APPLIED')
else:
    print('\nDRY RUN (pass --apply to write)')
