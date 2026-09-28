import json
import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Load source
with open('in_data/ja/g2.json', 'r', encoding='utf-8') as f:
    src = json.load(f)

src_strings = src['strings']
src_ids = [s['id'] for s in src_strings]
src_sc = {s['id']: s['en'].count(';') for s in src_strings}

print(f"Source: {len(src_strings)} strings, {sum(src_sc.values())} total semicolons")

# Merge all batch outputs
all_results = []
for i in range(7):
    path = f'tmp_out_{i}.json'
    try:
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        results = data.get('results', [])
        print(f"Batch {i}: {len(results)} strings")
        all_results.extend(results)
    except Exception as e:
        print(f"Batch {i} ERROR: {e}")

print(f"\nMerged total: {len(all_results)} strings")

# Validate
out_ids = [r['id'] for r in all_results]
errors = []

# Check all IDs present
for sid in src_ids:
    if sid not in out_ids:
        errors.append(f"Missing id: {sid}")

# Check no extra IDs
for oid in out_ids:
    if oid not in src_ids:
        errors.append(f"Extra id: {oid}")

# Check order matches
if src_ids == out_ids:
    print("ID order: OK")
else:
    errors.append("ID order mismatch")

# Check semicolon counts
sc_mismatches = 0
for r in all_results:
    rid = r['id']
    expected = src_sc.get(rid, -1)
    actual = r['tx'].count(';')
    if expected != actual and expected >= 0:
        sc_mismatches += 1
        if sc_mismatches <= 5:
            errors.append(f"Semicolon mismatch id {rid}: expected {expected}, got {actual}")

if sc_mismatches > 0:
    errors.append(f"Total semicolon mismatches: {sc_mismatches}")

total_sc_out = sum(r['tx'].count(';') for r in all_results)
print(f"Output semicolons: {total_sc_out} (expected {sum(src_sc.values())})")

if errors:
    print(f"\n--- ERRORS ({len(errors)}) ---")
    for e in errors[:20]:
        print(f"  {e}")
    sys.exit(1)
else:
    # Write final output
    with open('out_data/ja/g2.json', 'w', encoding='utf-8') as f:
        json.dump({'results': all_results}, f, ensure_ascii=False, indent=1)
    print("\nALL OK. Output written to out_data/ja/g2.json")
    print(f"Total strings: {len(all_results)}")
    print(f"Total semicolons: {total_sc_out}")
