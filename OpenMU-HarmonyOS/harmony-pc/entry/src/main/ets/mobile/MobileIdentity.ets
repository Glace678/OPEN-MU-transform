// ArkTS port of Android's MobileIdentity.java (HMAC-SHA256 over the pairing
// key). Identical computation to the phone build so auto-login targets the same
// account.
const LOGIN_NAME_CONTEXT = 'OpenMU-Mobile.LoginName.v1';
const LOGIN_PASSWORD_CONTEXT = 'OpenMU-Mobile.LoginPassword.v1';

export interface Credentials {
  username: string;
  password: string;
}

function utf8(text: string): Uint8Array {
  return new TextEncoder().encode(text);
}

const K: number[] = [
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
  0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
  0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
  0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
  0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
  0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
];

function rotr(x: number, n: number): number {
  return ((x >>> n) | (x << (32 - n))) >>> 0;
}

function sha256(message: Uint8Array): Uint8Array {
  const h: number[] = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
  const bitLen = message.length * 8;
  const paddedLen = (((message.length + 8) >> 6) + 1) * 64;
  const data = new Uint8Array(paddedLen);
  data.set(message);
  data[message.length] = 0x80;
  const view = new DataView(data.buffer);
  view.setUint32(paddedLen - 4, bitLen >>> 0, false);
  view.setUint32(paddedLen - 8, Math.floor(bitLen / 0x100000000), false);

  const w = new Int32Array(64);
  for (let offset = 0; offset < paddedLen; offset += 64) {
    for (let i = 0; i < 16; i++) {
      w[i] = view.getInt32(offset + i * 4, false);
    }
    for (let i = 16; i < 64; i++) {
      const s0 = rotr(w[i - 15], 7) ^ rotr(w[i - 15], 18) ^ (w[i - 15] >>> 3);
      const s1 = rotr(w[i - 2], 17) ^ rotr(w[i - 2], 19) ^ (w[i - 2] >>> 10);
      w[i] = (w[i - 16] + s0 + w[i - 7] + s1) | 0;
    }
    let a = h[0], b = h[1], c = h[2], d = h[3];
    let e = h[4], f = h[5], g = h[6], hh = h[7];
    for (let i = 0; i < 64; i++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (hh + S1 + ch + K[i] + w[i]) | 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) | 0;
      hh = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
    }
    h[0] = (h[0] + a) | 0; h[1] = (h[1] + b) | 0; h[2] = (h[2] + c) | 0; h[3] = (h[3] + d) | 0;
    h[4] = (h[4] + e) | 0; h[5] = (h[5] + f) | 0; h[6] = (h[6] + g) | 0; h[7] = (h[7] + hh) | 0;
  }
  const out = new Uint8Array(32);
  const outView = new DataView(out.buffer);
  for (let i = 0; i < 8; i++) {
    outView.setUint32(i * 4, h[i] >>> 0, false);
  }
  return out;
}

function hmacSha256(key: Uint8Array, message: Uint8Array): Uint8Array {
  const blockSize = 64;
  let normKey = key;
  if (key.length > blockSize) {
    normKey = sha256(key);
  }
  const padded = new Uint8Array(blockSize);
  padded.set(normKey);
  const ipad = new Uint8Array(blockSize + message.length);
  const opad = new Uint8Array(blockSize);
  for (let i = 0; i < blockSize; i++) {
    ipad[i] = padded[i] ^ 0x36;
    opad[i] = padded[i] ^ 0x5c;
  }
  ipad.set(message, blockSize);
  const inner = sha256(ipad);
  const outer = new Uint8Array(blockSize + inner.length);
  outer.set(opad, 0);
  outer.set(inner, blockSize);
  return sha256(outer);
}

function toUpperHex(bytes: Uint8Array): string {
  const digits = '0123456789ABCDEF';
  let result = '';
  for (let i = 0; i < bytes.length; i++) {
    result += digits[(bytes[i] >>> 4) & 0x0f] + digits[bytes[i] & 0x0f];
  }
  return result;
}

function toBase64UrlNoPad(bytes: Uint8Array): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
  let result = '';
  let i = 0;
  for (; i + 2 < bytes.length; i += 3) {
    const n = (bytes[i] << 16) | (bytes[i + 1] << 8) | bytes[i + 2];
    result += alphabet[(n >>> 18) & 0x3f] + alphabet[(n >>> 12) & 0x3f] + alphabet[(n >>> 6) & 0x3f] + alphabet[n & 0x3f];
  }
  const remaining = bytes.length - i;
  if (remaining === 1) {
    const n = bytes[i] << 16;
    result += alphabet[(n >>> 18) & 0x3f] + alphabet[(n >>> 12) & 0x3f];
  } else if (remaining === 2) {
    const n = (bytes[i] << 16) | (bytes[i + 1] << 8);
    result += alphabet[(n >>> 18) & 0x3f] + alphabet[(n >>> 12) & 0x3f] + alphabet[(n >>> 6) & 0x3f];
  }
  return result;
}

export function deriveCredentials(packageKey: string): Credentials {
  if (!/^[A-Za-z0-9_-]{43}$/.test(packageKey)) {
    throw new Error('Invalid mobile package key');
  }
  // The committed build-profile placeholder is 43 'A' characters. It passes the
  // format check above, so without this guard a debug build silently derives a
  // *different* account from the one the server holds -- auto-login then fails
  // with no hint about the cause. Fail loudly instead. The real key is injected
  // into build-profile.json5 at build time (see BUILD-鸿蒙构建说明.md).
  if (packageKey === 'A'.repeat(43)) {
    throw new Error('Mobile package key is the debug placeholder. Set the real ' +
      '43-character key in entry/build-profile.json5 (MOBILE_PACKAGE_KEY) so it ' +
      'matches the server.');
  }
  const key = utf8(packageKey);
  const usernameHex = toUpperHex(hmacSha256(key, utf8(LOGIN_NAME_CONTEXT)));
  const passwordB64 = toBase64UrlNoPad(hmacSha256(key, utf8(LOGIN_PASSWORD_CONTEXT)));
  return {
    username: 'mob' + usernameHex.substring(0, 7),
    password: passwordB64.substring(0, 20)
  };
}
