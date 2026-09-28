# -*- coding: utf-8 -*-
"""Report translation coverage per language/chunk against expected input ids."""
import json, os, sys, glob

WORK = os.path.dirname(os.path.abspath(__file__))
LANGS = ['de', 'id', 'ja', 'pl', 'ru', 'tl', 'uk']
CHUNKS = ['g1', 'g2', 'g3a', 'g3b', 'g4', 'g5']

def expected():
    exp = {}
    d = os.path.join(WORK, 'in_data', 'de')
    for g in CHUNKS:
        data = json.load(open(os.path.join(d, g + '.json'), encoding='utf-8'))
        exp[g] = [s['id'] for s in data['strings']]
    return exp

def main():
    exp = expected()
    for lang in LANGS:
        print(f'== {lang}')
        have = {}
        for p in glob.glob(os.path.join(WORK, 'out_data', lang, '*.json')):
            base = os.path.splitext(os.path.basename(p))[0]
            if base.startswith('_'):
                continue
            try:
                data = json.load(open(p, encoding='utf-8'))
                have[base] = {int(r['id']) for r in data.get('results', [])
                              if r.get('tx') is not None and str(r.get('tx')).strip()}
            except Exception as e:
                have[base] = set()
                print(f'   {base}: UNREADABLE {e}')
        for g in CHUNKS:
            want = set(exp[g])
            got = have.get(g, set())
            ok = len(want & got)
            missing = want - got
            extra = got - want
            status = 'OK ' if not missing else 'MISS'
            print(f'   {g:4s} {status} {ok}/{len(want)}'
                  + (f' missing={len(missing)}' if missing else '')
                  + (f' extra={len(extra)}' if extra else ''))
            if missing and len(missing) <= 10:
                print('        ids:', sorted(missing))

if __name__ == '__main__':
    main()
