# -*- coding: utf-8 -*-
"""Inspect and unpack the self-contained pack format used by OpenMU-Local.exe.

The pack is a chain of fixed-layout records laid out at the end of the exe:

    [ offset:8 ][ size:8 ][ c:8 ][ type:1 ][ name-len:varint ][ name:bytes ]

walked *backwards* from the file tail. There is no central directory or magic
signature, so the only reliable entry point is the last record: the name field
is self-describing enough that each step can be validated, and a broken link
stops the walk instead of emitting garbage.

Subcommands:
    list     print the record table
    probe    print a summary plus hex dumps around boundary records
    extract  write the embedded files to a directory

The historical scripts (analyze_pack / parse_manifest / extract_pack /
extract_one / _extract_core) each hard-coded a different subset of this
behaviour, and two of them pinned a magic start offset that was really
"size of the backup exe - 24" -- it silently went stale the moment the exe
was rebuilt. This tool always starts at the file tail, so it cannot rot.
"""

import argparse
import os
import re
import struct
import sys

NAME_RE = re.compile(
    rb"^[A-Za-z0-9_\-./]+\.(dll|json|exe|dat|db|pdb|config|xml|so|dylib|bin)$"
)

# Runtime/framework files that every pack contains; --app-files filters these out.
RUNTIME_RE = re.compile(
    r"^(System\.|Microsoft\.|WinRT|zh-|api-|hostfxr|hostpolicy|coreclr|clrjit|clr|"
    r"mscor|ucrt|vcruntime|api-ms)",
    re.I,
)

RECORD_HEADER_SIZE = 24  # offset, size, c  (three little-endian uint64)
MIN_NAME_LEN = 4
MAX_NAME_LEN = 100


class Record:
    __slots__ = ("name", "offset", "size", "extra", "type", "header_pos")

    def __init__(self, name, offset, size, extra, type_, header_pos):
        self.name = name
        self.offset = offset
        self.size = size
        self.extra = extra
        self.type = type_
        self.header_pos = header_pos

    @property
    def end(self):
        return self.offset + self.size


def _read_header(data, pos):
    """Validate one [type][len][name] header at `pos`; return (type, name, end)."""
    if pos + 2 >= len(data):
        return None
    type_ = data[pos]
    name_len = data[pos + 1]
    if not (0 <= type_ <= 10 and MIN_NAME_LEN <= name_len <= MAX_NAME_LEN):
        return None
    name = data[pos + 2 : pos + 2 + name_len]
    if not NAME_RE.match(name):
        return None
    return type_, name.decode(), pos + 2 + name_len


def parse(data):
    """Walk the record chain backwards from the file tail; returns records in file order."""
    records = []
    pos = len(data) - RECORD_HEADER_SIZE
    while pos > 0:
        header = _read_header(data, pos)
        if header is None:
            break
        type_, name, _name_end = header
        fixed_pos = pos - RECORD_HEADER_SIZE
        if fixed_pos < 0:
            break
        try:
            offset, size, extra = struct.unpack_from("<QQQ", data, fixed_pos)
        except struct.error:
            break
        records.append(Record(name, offset, size, extra, type_, pos))

        # The next header sits immediately before this record's fixed fields.
        # Its length is unknown, so probe every plausible length until the
        # candidate's name ends exactly where the fixed fields begin.
        pos = None
        for name_len in range(MIN_NAME_LEN, MAX_NAME_LEN + 1):
            candidate = fixed_pos - (2 + name_len)
            probe = _read_header(data, candidate)
            if probe is not None and probe[2] == fixed_pos:
                pos = candidate
                break
        if pos is None:
            print(
                f"warning: record chain stops before '{name}' "
                f"(fixed fields at {fixed_pos:#x}); "
                f"preceding bytes: {data[fixed_pos - 32 : fixed_pos].hex(' ')}",
                file=sys.stderr,
            )
            break

    records.reverse()
    return records


def _bytes_at(data, pos, count=4):
    if pos < 0 or pos + count > len(data):
        return b"<OOB>"
    return data[pos : pos + count]


def cmd_list(args, data):
    records = parse(data)
    if not records:
        sys.exit("no records found; not a pack file?")
    print(f"records: {len(records)}; first header @ {records[0].header_pos:#x}")
    print(
        f"\n{'#':>3} {'type':>4} {'name':45s} {'offset':>12s} "
        f"{'size':>10s} {'extra':>10s}  magic@offset"
    )
    for i, r in enumerate(records):
        print(
            f"{i:3d} {r.type:4d} {r.name[:44]:45s} {r.offset:12d} "
            f"{r.size:10d} {r.extra:10d}  {_bytes_at(data, r.offset)}"
        )

    ordered = sorted(records, key=lambda r: r.offset)
    print("\nlowest payload offset:", hex(ordered[0].offset))
    highest = ordered[-1]
    print(
        f"highest payload end: {highest.end:#x} (file size {len(data)}, "
        f"slack {len(data) - highest.end})"
    )
    return 0


def cmd_probe(args, data):
    records = parse(data)
    print("file size:", len(data))
    print("records found:", len(records))
    if not records:
        return 1
    for r in records[: args.records]:
        print(f"  @{r.header_pos} type={r.type} size={r.size} {r.name}")
    if len(records) > args.records * 2:
        print("  ...")
        for r in records[-args.records :]:
            print(f"  @{r.header_pos} type={r.type} size={r.size} {r.name}")

    for r in records[: 2] + records[-2 :]:
        print(f"\n--- {r.name} @{r.header_pos} ---")
        chunk = data[r.header_pos - 8 : r.header_pos + args.bytes]
        base = r.header_pos - 8
        for row in range(0, len(chunk), 16):
            line = chunk[row : row + 16]
            hexs = " ".join(f"{b:02x}" for b in line)
            text = "".join(chr(b) if 32 <= b < 127 else "." for b in line)
            print(f"{base + row:12d} ({base + row:#010x}): {hexs:<48} {text}")
    return 0


def _want(record, only, exact, app_files):
    if exact and os.path.basename(record.name) not in exact:
        return False
    if only and not any(record.name.endswith(suf) for suf in only):
        return False
    if app_files and RUNTIME_RE.match(record.name):
        return False
    return True


def cmd_extract(args, data):
    records = parse(data)
    only = [s for s in args.only.split(",") if s] if args.only else None
    exact = {s for s in args.exact.split(",") if s} if args.exact else None
    wanted = [r for r in records if _want(r, only, exact, args.app_files)]
    print(f"extracting {len(wanted)} of {len(records)} files -> {args.outdir}")
    os.makedirs(args.outdir, exist_ok=True)
    written = 0
    for r in wanted:
        if r.offset + r.size > len(data):
            print(f"  SKIP out-of-range {r.name} @{r.offset} size={r.size}", file=sys.stderr)
            continue
        dest = os.path.join(args.outdir, r.name.replace("/", os.sep))
        os.makedirs(os.path.dirname(dest) or args.outdir, exist_ok=True)
        with open(dest, "wb") as handle:
            handle.write(data[r.offset : r.end])
        written += 1
        if args.verbose:
            print(f"  t{r.type} {r.size:>9d}  {r.name}")
    print(f"wrote {written} files")
    return 0 if written else 1


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("exe", help="path to the packed executable")

    p_list = sub.add_parser("list", parents=[common], help="print the record table")
    p_list.set_defaults(func=cmd_list)

    p_probe = sub.add_parser("probe", parents=[common], help="summary + hex dumps")
    p_probe.add_argument("--records", type=int, default=6, help="records to list (default 6)")
    p_probe.add_argument("--bytes", type=int, default=80, help="bytes to dump per record")
    p_probe.set_defaults(func=cmd_probe)

    p_extract = sub.add_parser("extract", parents=[common], help="write embedded files to a dir")
    p_extract.add_argument("outdir")
    p_extract.add_argument(
        "--only", help="comma-separated path suffixes to keep (e.g. .dll,.json)"
    )
    p_extract.add_argument(
        "--exact", help="comma-separated file basenames to keep (e.g. OpenMU-Local.dll)"
    )
    p_extract.add_argument(
        "--app-files", action="store_true", help="skip .NET/runtime framework files"
    )
    p_extract.add_argument("-v", "--verbose", action="store_true")
    p_extract.set_defaults(func=cmd_extract)

    args = parser.parse_args(argv)
    try:
        with open(args.exe, "rb") as handle:
            data = handle.read()
    except OSError as exc:
        sys.exit(f"cannot read {args.exe}: {exc}")
    return args.func(args, data)


if __name__ == "__main__":
    sys.exit(main())
