#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))
import config
"""MuMain BMD toolkit.

Two on-disk layouts:
  * fixed records  : N records of R bytes, each record independently XORed with
                     the 3-byte BuxConvert key (key restarts at every record),
                     optional trailing DWORD checksum = GenerateCheckSum2 over
                     the ENCRYPTED buffer.
  * variable records (QuestWords): [int index][short len][len bytes text], the
                     6-byte header and the text blob are XORed separately (key
                     restarts for each).

Text inside decrypted records is UTF-8 (NUL-padded fixed char arrays).
"""
import struct, sys, os

KEY = bytes([0xFC, 0xCF, 0xAB])

def bux(buf):
    """In-place XOR with the 3-byte key over the whole buffer (key index 0..)."""
    for i in range(len(buf)):
        buf[i] ^= KEY[i % 3]
    return buf

def bux_copy(b):
    a = bytearray(b)
    return bux(a)

def gen_checksum2(data: bytes, wkey: int) -> int:
    """Port of GenerateCheckSum2 (little-endian DWORD reads)."""
    dwkey = wkey & 0xFFFF
    result = (dwkey << 9) & 0xFFFFFFFF
    size = len(data)
    dw = 0
    while dw <= size - 4:
        temp = struct.unpack_from('<I', data, dw)[0]
        if ((dw // 4 + wkey) % 2) == 0:
            result ^= temp
        else:
            result = (result + temp) & 0xFFFFFFFF
        if (dw % 16) == 0:
            shift = ((dw // 4) % 8) + 1
            result ^= (((dwkey + result) & 0xFFFFFFFF) >> shift)
        result &= 0xFFFFFFFF
        dw += 4
    return result

class FixedFile:
    def __init__(self, path, R, N, key=None, has_checksum=True):
        self.path = path; self.R = R; self.N = N; self.key = key
        self.raw = open(path, 'rb').read()
        body = self.raw
        self.stored_checksum = None
        if has_checksum:
            if len(self.raw) == R*N + 4:
                self.stored_checksum = struct.unpack_from('<I', self.raw, R*N)[0]
                body = self.raw[:R*N]
            elif len(self.raw) == R*N:
                body = self.raw
            else:
                # try to detect
                if len(self.raw) >= R*N:
                    self.stored_checksum = struct.unpack_from('<I', self.raw, R*N)[0]
                    body = self.raw[:R*N]
                else:
                    raise ValueError(f"size {len(self.raw)} < {R*N}")
        else:
            body = self.raw[:R*N]
        assert len(body) == R*N, f"body {len(body)} != {R*N}"
        # decrypt per record
        self.recs = []
        enc = bytearray(body)
        for i in range(N):
            rec = bytearray(enc[i*R:(i+1)*R])
            bux(rec)
            self.recs.append(rec)
        self.enc = bytes(enc)

    def verify(self):
        if self.key is None or self.stored_checksum is None:
            return None
        calc = gen_checksum2(self.enc, self.key)
        return calc == self.stored_checksum, calc, self.stored_checksum

    def get_str(self, i, off, maxlen):
        rec = self.recs[i]
        end = rec.find(b'\x00', off, off+maxlen)
        if end < 0: end = off+maxlen
        raw = bytes(rec[off:end])
        try:
            return raw.decode('utf-8')
        except UnicodeDecodeError:
            return raw.decode('latin-1')

    def set_str(self, i, off, maxlen, text):
        """Write UTF-8 text into fixed field, NUL-padded. Raises if too long."""
        b = text.encode('utf-8')
        if len(b) >= maxlen:
            raise ValueError(f"text too long: {len(b)} >= {maxlen} for {text!r}")
        rec = self.recs[i]
        rec[off:off+maxlen] = b'\x00'*maxlen
        rec[off:off+len(b)] = b

    def encode(self):
        out = bytearray()
        for rec in self.recs:
            r = bytearray(rec)
            bux(r)
            out += r
        if self.key is not None:
            cs = gen_checksum2(bytes(out), self.key)
            out += struct.pack('<I', cs)
        return bytes(out)

# ---------- QuestWords variable records ----------
def read_questwords(path):
    data = open(path,'rb').read()
    recs = []
    pos = 0
    while pos < len(data):
        hdr = bytearray(data[pos:pos+6]); bux(hdr)
        index, wlen = struct.unpack('<ih', bytes(hdr))
        pos += 6
        txt = bytearray(data[pos:pos+wlen]); bux(txt)
        pos += wlen
        recs.append((index, bytes(txt)))
    return recs, len(data)

def write_questwords(path, recs):
    out = bytearray()
    for index, txt in recs:
        if isinstance(txt, str): txt = txt.encode('utf-8')
        hdr = bytearray(struct.pack('<ih', index, len(txt))); bux(hdr)
        body = bytearray(txt); bux(body)
        out += hdr + body
    open(path,'wb').write(bytes(out))
    return len(out)

if __name__ == '__main__':
    # quick self-test on item
    p = sys.argv[1] if len(sys.argv)>1 else os.path.join(config.SERVER.chs, 'item_chs.bmd')
    f = FixedFile(p, 84, 8192, key=0xE2F1)
    print("size", len(f.raw), "checksum", f.verify())
    for i in range(6):
        rec = f.recs[i]
        print(i, rec[:60])
