# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""After translation: finalize coverage, then build all 7 MT locale data trees."""
import json, os, subprocess, sys

WORK = W
PY = sys.executable
LANGS = ['de','id','ja','pl','ru','tl','uk']

# 1) finalize (also validates full coverage)
print('### FINALIZE')
r = subprocess.run([PY, os.path.join(WORK,'finalize_data.py')], cwd=WORK)
if r.returncode != 0:
    print('!! finalize reports incomplete coverage; aborting build'); sys.exit(1)

# 2) build each locale
summary={}
for lang in LANGS:
    print(f'\n### BUILD {lang}')
    r = subprocess.run([PY, os.path.join(WORK,'build_locale_data.py'), lang, '--write'], cwd=WORK)
    if r.returncode != 0:
        print(f'!! build failed for {lang}'); sys.exit(2)
print('\nAll locale data trees built under', os.path.join(WORK,'build'))
