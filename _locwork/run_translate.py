#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os as _os
W = _os.path.dirname(_os.path.abspath(__file__))
"""Main runner: combine translations, generate output JSON, validate."""
import json
import sys
import os

INPUT = os.path.join(W, 'in_data', 'de', 'g1.json')
OUTPUT = os.path.join(W, 'out_data', 'de', 'g1.json')

# Import both translation dictionaries
sys.path.insert(0, W)

# Load translations by executing the files
T = {}
exec(open(os.path.join(W, 'translations_part1.py'), encoding='utf-8').read())
T1 = T.copy()
exec(open(os.path.join(W, 'translations_part2.py'), encoding='utf-8').read())
T2 = T.copy()

# Merge
T = {**T1, **T2}
print(f"Total translations loaded: {len(T)}")

# Load input
with open(INPUT, 'r', encoding='utf-8') as f:
    data = json.load(f)

input_count = len(data['strings'])
print(f"Input strings: {input_count}")

# Check for missing translations
missing = [s['id'] for s in data['strings'] if s['id'] not in T]
print(f"Missing translations: {len(missing)}")
if missing:
    print(f"Missing ids: {missing[:50]}")
    if len(missing) > 50:
        print(f"... and {len(missing)-50} more")

# Build results
results = []
issues = []

for s in data['strings']:
    sid = s['id']
    en = s['en']
    budget = s['budget']

    if sid in T:
        tx = T[sid]
    else:
        # Fallback: pass through English (should not happen)
        tx = en
        issues.append(f"MISSING id={sid}")

    # Validate semicolon count
    en_sc = en.count(';')
    tx_sc = tx.count(';')
    if en_sc != tx_sc:
        issues.append(f"SEMICOLON MISMATCH id={sid}: en={en_sc} tx={tx_sc}")

    # Validate byte length
    tx_bytes = len(tx.encode('utf-8'))
    if tx_bytes > budget:
        issues.append(f"BUDGET EXCEEDED id={sid}: {tx_bytes} > {budget}")

    # Verify printf tokens preserved (simple check)
    import re
    en_tokens = set(re.findall(r'%[\d.]*[dfsuf%]|%[0-9]', en))
    tx_tokens = set(re.findall(r'%[\d.]*[dfsuf%]|%[0-9]', tx))
    if en_tokens != tx_tokens:
        issues.append(f"PRINTF MISMATCH id={sid}: en={en_tokens} tx={tx_tokens}")

    results.append({"id": sid, "tx": tx})

# Write output
output = {"results": results}
os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
with open(OUTPUT, 'w', encoding='utf-8', newline='') as f:
    json.dump(output, f, ensure_ascii=False, indent=1)

# Verify round-trip
with open(OUTPUT, 'r', encoding='utf-8') as f:
    check = json.load(f)

output_count = len(check['results'])
print(f"Output results: {output_count}")

# Verify all ids match exactly
input_ids = [s['id'] for s in data['strings']]
output_ids = [r['id'] for r in check['results']]
if input_ids == output_ids:
    print("IDs verified: all match ✓")
else:
    print("WARNING: ID mismatch!")
    missing_in_out = set(input_ids) - set(output_ids)
    extra_in_out = set(output_ids) - set(input_ids)
    if missing_in_out:
        print(f"  Missing in output: {sorted(missing_in_out)[:20]}")
    if extra_in_out:
        print(f"  Extra in output: {sorted(extra_in_out)[:20]}")

# Check for BOM
with open(OUTPUT, 'rb') as f:
    bom = f.read(3)
    if bom == b'\xef\xbb\xbf':
        print("WARNING: File has BOM!")
    else:
        print("BOM check: no BOM ✓")

# Report issues
if issues:
    print(f"\n--- ISSUES ({len(issues)}) ---")
    for issue in issues[:30]:
        print(f"  {issue}")
    if len(issues) > 30:
        print(f"  ... and {len(issues)-30} more")
else:
    print("All checks passed ✓")

print(f"\nOutput written to: {OUTPUT}")
