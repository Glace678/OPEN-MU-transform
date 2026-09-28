# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""Per-format BMD decode/encode + string-slot extraction for MuMain localization.

Every format provides:
  .slots : list of dicts {id, text}             current UTF-8 text ('' if empty)
  .set_text(slot_id, new_text)                  write Chinese (fits, NUL-padded)
  .encode() -> bytes                            reproduce the file (must round-trip)

Translatable inventory (Chs language folder), formats derived from the C++ loaders:
  item            R=84  N=8192 cs=E2F1  name@0:30
  skill           R=108 N=650  cs=5A18  name@0:32
  quest           R=744 N=200  no cs     name@6:32
  movereq         [count] R=84 N=count no cs  main@4:32 sub@36:32
  socket          R=104 N=150  no cs     name@8:64
  setoption       R=110 N=64   cs=A2F1   name@0:64
  buff            [count=158] R=158 cs=E2F1  name@5:50 desc@58:100 ('/'-sep)
  harmony         R=180 N=30   no cs     name@4:60   (whole-XOR == per-record)
  mastertooltip   R=616 N=512  +4(unverified)  Info1..7 (hardcoded Eng path)
  minimap         R=116 N=100  +45 trailer cs=2BC1  name@16:100
  slide           single 41008 struct, whole-XOR, slots len256
  questwords      variable records [int index][short len][utf8]  (NPC dialogue)
"""
import struct, os
from bmd import bux, gen_checksum2, read_questwords, write_questwords

CHS = config.SERVER.chs
ENG = config.SERVER.eng


def _u8(b):
    e = b.find(b'\x00')
    if e >= 0:
        b = b[:e]
    try:
        return b.decode('utf-8')
    except UnicodeDecodeError:
        return b.decode('latin-1')


class FixedRecs:
    """Generic fixed-record file. records are decrypted bytearrays (per-record XOR)."""
    def __init__(self, path, R, N, key=None, count_prefix=False, trailer=0):
        self.path, self.R, self.N, self.key = path, R, N, key
        self.count_prefix = count_prefix
        self.trailer = trailer
        raw = open(path, 'rb').read()
        self.raw = raw
        pos = 0
        if count_prefix:
            (self.count,) = struct.unpack_from('<I', raw, 0)
            pos = 4
            self.N = self.count
        body_end = pos + R * self.N
        self.body = bytearray(raw[pos:body_end])
        self.trailer_bytes = bytearray(raw[body_end:])  # checksum and/or raw trailer
        # decrypt per record
        self.recs = []
        for i in range(self.N):
            rec = bytearray(self.body[i*R:(i+1)*R])
            bux(rec)
            self.recs.append(rec)

    def field(self, i, off, ln):
        return _u8(bytes(self.recs[i][off:off+ln]))

    def set_field(self, i, off, ln, text):
        b = text.encode('utf-8')
        if len(b) >= ln:
            raise ValueError(f'text {len(b)}B >= field {ln}: {text!r}')
        rec = self.recs[i]
        rec[off:off+ln] = b'\x00'*ln
        rec[off:off+len(b)] = b

    def encode(self):
        prefix = bytearray()
        if self.count_prefix:
            prefix += struct.pack('<I', self.count)
        body = bytearray()
        for rec in self.recs:
            r = bytearray(rec); bux(r); body += r
        raw_trailer = bytes(self.trailer_bytes[:self.trailer])
        if self.key is not None:
            # checksum is computed over everything fread before the checksum DWORD:
            # encrypted body (+ any raw trailer bytes, e.g. minimap's 45)
            cs = gen_checksum2(bytes(body) + raw_trailer, self.key)
            return bytes(prefix) + bytes(body) + raw_trailer + struct.pack('<I', cs)
        return bytes(prefix) + bytes(body) + bytes(self.trailer_bytes)


# ---- per-format slot definitions: (kind, off, ln) for text fields in a record --
FMT = {
    'item':    dict(R=84,  N=8192, key=0xE2F1, fields=[('name',0,30)]),
    'skill':   dict(R=108, N=650,  key=0x5A18, fields=[('name',0,32)]),
    'quest':   dict(R=744, N=200,  key=None,   fields=[('name',6,32)]),
    'movereq': dict(R=84,  N=None, key=None, count_prefix=True,
                    fields=[('main',4,32),('sub',36,32)]),
    'socket':  dict(R=104, N=150,  key=None,   fields=[('name',8,64)]),
    'setoption':dict(R=110,N=64,   key=0xA2F1, fields=[('name',0,64)]),
    'buff':    dict(R=158, N=None, key=0xE2F1, count_prefix=True,
                    fields=[('name',5,50),('desc',58,100)]),
    'harmony': dict(R=180, N=30,   key=None,   fields=[('name',4,60)]),
    'mastertooltip': dict(R=616, N=512, key='KEEP',  # header: int SkillNumber + WORD ClassCode = 6 bytes
                    fields=[('i1',6,64),('i2',70,256),('i3',326,32),
                            ('i4',358,64),('i5',422,64),('i6',486,64),('i7',550,64)]),
    'minimap': dict(R=116, N=100,  key=0x2BC1, trailer=45, fields=[('name',16,100)]),
}

FILES = {
    'item':    f'{CHS}\\item_chs.bmd',
    'skill':   f'{CHS}\\skill_chs.bmd',
    'quest':   f'{CHS}\\Quest_chs.bmd',
    'movereq': f'{CHS}\\MoveReq_chs.bmd',
    'socket':  f'{CHS}\\socketitem_chs.bmd',
    'setoption': f'{CHS}\\itemsetoption_chs.bmd',
    'buff':    f'{CHS}\\BuffEffect_chs.bmd',
    'harmony': f'{CHS}\\JewelOfHarmonyOption_chs.bmd',
    'mastertooltip': f'{CHS}\\MasterSkillTooltip_chs.bmd',  # client now loads per-language (was hardcoded Eng)
    'minimap': None,  # 20 files, handled separately
}


def open_fmt(kind, path=None):
    c = FMT[kind]
    path = path or FILES[kind]
    key = c['key']
    if key == 'KEEP':
        key = None  # checksum present but not verified by client; keep trailing bytes
    return FixedRecs(path, c['R'], c['N'] if c['N'] is not None else 0,
                     key=key, count_prefix=c.get('count_prefix', False),
                     trailer=c.get('trailer', 0))


def roundtrip(kind, path=None):
    c = FMT[kind]
    path = path or FILES[kind]
    f = open_fmt(kind, path)
    out = f.encode()
    raw = open(path, 'rb').read()
    return out == raw, len(out), len(raw)


# ---- slide (single whole-XOR struct) ----
SLIDE_SIZE = 41008
def slide_slot_offset(level, j):
    return 8 + level*8200 + 8 + j*256

def open_slide(path=f'{CHS}\\slide_chs.bmd'):
    raw = open(path,'rb').read()
    buf = bytearray(raw); bux(buf)
    return raw, buf

def slide_slots(buf):
    out = []
    for L in range(5):
        base = 8 + L*8200
        iNumber = struct.unpack_from('<i', buf, base+4)[0]
        for j in range(32):
            off = base + 8 + j*256
            txt = _u8(bytes(buf[off:off+256]))
            if txt:
                out.append((L, j, iNumber, off, txt))
    return out

def encode_slide(buf):
    b = bytearray(buf); bux(b); return bytes(b)


if __name__ == '__main__':
    print('=== fixed-record round-trips ===')
    for kind in ['item','skill','quest','movereq','socket','setoption','buff','harmony','mastertooltip']:
        ok,a,b = roundtrip(kind)
        print(f'  {kind:14s} {"OK " if ok else "FAIL"} {a}=={b}')
    # minimap
    import glob
    mm = sorted(glob.glob(f'{CHS}\\Minimap\\*.bmd'))
    oks = all(roundtrip('minimap', p)[0] for p in mm)
    print(f'  {"minimap":14s} {"OK " if oks else "FAIL"} ({len(mm)} files)')
    # slide
    raw,buf = open_slide()
    print(f'  {"slide":14s} {"OK " if encode_slide(buf)==raw else "FAIL"} {len(raw)}')
    # questwords
    qw,sz = read_questwords(f'{CHS}\\QuestWords_chs.bmd')
    print(f'  {"questwords":14s} records={len(qw)} bytes={sz}')
