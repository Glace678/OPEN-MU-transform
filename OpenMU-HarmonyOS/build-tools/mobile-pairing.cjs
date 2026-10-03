'use strict';

function isPrivateAddress(address) {
  if (address.toLowerCase() === 'localhost') return true;
  const parts = address.split('.');
  if (parts.length !== 4 || parts.some(part => !/^(0|[1-9][0-9]{0,2})$/.test(part))) return false;
  const octets = parts.map(Number);
  if (octets.some(octet => octet > 255)) return false;
  const [a, b] = octets;
  return a === 127 || a === 10 || (a === 172 && b >= 16 && b <= 31)
    || (a === 192 && b === 168) || (a === 169 && b === 254);
}

function validateServer(fields, kind) {
  if (kind === 'game' || kind === 'pc') {
    const address = fields.DEFAULT_SERVER_ADDRESS;
    if (typeof address !== 'string' || !isPrivateAddress(address)) {
      // Cloud-server builds opt in explicitly. The game channel is a plain TCP
      // socket, so this is only acceptable behind a TLS-protected tunnel; the
      // escape hatch exists so the decision is loud, never silent.
      if (process.env.OPENMU_ALLOW_PUBLIC_SERVER === '1') {
        console.warn(`[openmu] DEFAULT_SERVER_ADDRESS=${address} is not a private address; `
          + 'OPENMU_ALLOW_PUBLIC_SERVER=1 is set. Ensure the game TCP channel is TLS-protected '
          + 'and that the account pairing is device-level before distributing this build.');
        return;
      }
      throw new Error(`DEFAULT_SERVER_ADDRESS must be localhost or a private IPv4 address `
        + `(got "${address}"). For a cloud server build, set OPENMU_ALLOW_PUBLIC_SERVER=1 `
        + 'and read the public-server notes in README-zh-CN.md first.');
    }
    return;
  }

  if (kind !== 'gm') throw new Error(`Unknown mobile package kind: ${kind}`);
  const raw = fields.DEFAULT_SERVER_URL;
  if (typeof raw !== 'string' || raw !== raw.trim() || !/^https?:\/\//i.test(raw)) {
    throw new Error('DEFAULT_SERVER_URL must be an absolute HTTP or HTTPS URL.');
  }
  let url;
  try { url = new URL(raw); } catch { throw new Error('DEFAULT_SERVER_URL is invalid.'); }
  if (!url.hostname || url.username || url.password || url.search || url.hash) {
    throw new Error('DEFAULT_SERVER_URL must not contain credentials, a query, or a fragment.');
  }
  // Compare the original host too: URL normalizes integer, octal and hexadecimal
  // IPv4 spellings, which the mobile clients deliberately do not accept.
  const authority = raw.substring(raw.indexOf('://') + 3).split('/')[0];
  const rawHost = authority.split(':')[0];
  if (url.protocol === 'http:' && (!isPrivateAddress(rawHost) || !isPrivateAddress(url.hostname))) {
    throw new Error('DEFAULT_SERVER_URL requires HTTPS outside localhost and private IPv4 networks.');
  }
}

function validateFields(fields, kind, allowPlaceholder) {
  const key = fields.MOBILE_PACKAGE_KEY;
  if (typeof key !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(key)) {
    throw new Error('MOBILE_PACKAGE_KEY must contain exactly 43 base64url characters.');
  }
  if (key === 'A'.repeat(43) && !allowPlaceholder) {
    throw new Error('Replace the placeholder MOBILE_PACKAGE_KEY before building; local tests may set OPENMU_ALLOW_PLACEHOLDER_MOBILE_KEY=1.');
  }
  validateServer(fields, kind);
}

function validateMobilePairing(profile, kind, allowPlaceholder = false) {
  const base = profile?.buildOption?.arkOptions?.buildProfileFields;
  if (!base || typeof base !== 'object' || Array.isArray(base)) {
    throw new Error('Missing buildOption.arkOptions.buildProfileFields.');
  }
  validateFields(base, kind, allowPlaceholder);
  for (const option of profile.buildOptionSet ?? []) {
    const overrides = option?.arkOptions?.buildProfileFields;
    if (overrides !== undefined) validateFields({ ...base, ...overrides }, kind, allowPlaceholder);
  }
}

module.exports = { validateMobilePairing };
