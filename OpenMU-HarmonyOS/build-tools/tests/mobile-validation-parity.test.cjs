'use strict';

// XC-19: runtime validator coverage for the HarmonyOS variants.
//
// The shipped validators live inside DevEco/ArkTS modules that cannot be
// imported from Node, so the pure functions are extracted directly from the
// .ets sources (brace-matched), stripped of their TS type annotations, and
// evaluated here. This executes the exact code that ships:
//   - MobileConfig.isValidServerAddress in BOTH harmony-game and harmony-pc,
//     run against the same matrix to pin parity;
//   - isAllowedServerUrl in the GM app's MobileGmClient.
// Source-level checks additionally pin that writeConfig actually invokes the
// address validator in each game variant, so the H-04 guard cannot be removed
// silently.

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.join(__dirname, '..', '..');

const GAME_CONFIG = path.join(
  repoRoot, 'harmony-game', 'entry', 'src', 'main', 'ets', 'mobile', 'MobileConfig.ets');
const PC_CONFIG = path.join(
  repoRoot, 'harmony-pc', 'entry', 'src', 'main', 'ets', 'mobile', 'MobileConfig.ets');
const GM_CLIENT = path.join(
  repoRoot, 'harmony-gm', 'entry', 'src', 'main', 'ets', 'model', 'MobileGmClient.ets');

// Return the substring for one top-level brace block starting at the first
// '{' at or after `startIndex`. The validator bodies contain no braces inside
// strings/regexes, so character counting is sufficient.
function readBraceBlock(source, startIndex) {
  const open = source.indexOf('{', startIndex);
  assert.notEqual(open, -1, 'opening brace not found');
  let depth = 0;
  for (let i = open; i < source.length; i++) {
    const ch = source[i];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return source.slice(open, i + 1);
    }
  }
  throw new Error('unterminated brace block');
}

// Extract a `static name(...)` method and compile it as a standalone function.
function loadStaticMethod(file, methodName) {
  const source = fs.readFileSync(file, 'utf8');
  const marker = `static ${methodName}`;
  const start = source.indexOf(marker);
  assert.notEqual(start, -1, `${methodName} not found in ${file}`);
  const body = readBraceBlock(source, start);
  const signature = source.slice(start, source.indexOf('{', start))
    .replace(/^static\s+/, '')
    .replace(`: boolean`, '');
  const fnText = `function ${signature} ${body}`
    .replace(`${methodName}(value: string)`, `${methodName}(value)`);
  return new Function(`${fnText}; return ${methodName};`)();
}

// Extract a top-level `export function name(...)` and compile it.
function loadExportedFunction(file, functionName) {
  const source = fs.readFileSync(file, 'utf8');
  const marker = `export function ${functionName}`;
  const start = source.indexOf(marker);
  assert.notEqual(start, -1, `${functionName} not found in ${file}`);
  const body = readBraceBlock(source, start);
  const signature = source.slice(start, source.indexOf('{', start))
    .replace(/^export\s+function\s+/, '')
    .replace(`: boolean`, '');
  const fnText = `function ${signature} ${body}`
    .replace(`${functionName}(rawUrl: string)`, `${functionName}(rawUrl)`)
    .replace(/:\s*number\[\]/g, '');
  return new Function(`${fnText}; return ${functionName};`)();
}

const gameIsValid = loadStaticMethod(GAME_CONFIG, 'isValidServerAddress');
const pcIsValid = loadStaticMethod(PC_CONFIG, 'isValidServerAddress');
const isAllowedServerUrl = loadExportedFunction(GM_CLIENT, 'isAllowedServerUrl');

const VALID_ADDRESSES = [
  '127.0.0.1',
  '192.168.1.2',
  '10.0.0.1',
  '172.16.0.1',
  '172.31.255.255',
  '169.254.0.1',
  '0.0.0.0',
  '255.255.255.255',
  'localhost',
  'example.com',
  'a-b.example.org',
  'MU.host-01',
  'xn--nw2a.xn--42b.example'
];

const INVALID_ADDRESSES = [
  '',
  '   ',
  '192.168.1.',
  '.example.com',
  'example.com.',
  'a..b.example',
  '-bad.example',
  'bad-.example',
  '256.1.1.1',
  '192.168.1.256',
  '01.1.1.1',
  '192.168.001.2',
  // Note: '1.2.3' is intentionally accepted - it is a syntactically valid
  // hostname under the label rules, even if not a four-octet IPv4 address.
  '1.2.3.4.',
  'exa mple.com',
  'under_score.example',
  'label-' + 'x'.repeat(61) + '.example' // 64-char label
];

test('game MobileConfig accepts well-formed IPv4 addresses and hostnames', () => {
  for (const address of VALID_ADDRESSES) {
    assert.equal(gameIsValid(address), true, `expected valid: ${address}`);
  }
});

test('game MobileConfig rejects empty labels, bad octets and leading-zero IPv4', () => {
  for (const address of INVALID_ADDRESSES) {
    assert.equal(gameIsValid(address), false, `expected invalid: "${address}"`);
  }
});

test('harmony-game and harmony-pc validators agree on every address (parity)', () => {
  // Whitespace trimming is part of the contract too.
  const matrix = VALID_ADDRESSES.concat(INVALID_ADDRESSES)
    .flatMap(a => [a, `  ${a} `, `${a}\t`]);
  for (const address of matrix) {
    assert.equal(pcIsValid(address), gameIsValid(address),
      `parity divergence on "${address}"`);
  }
});

test('GM isAllowedServerUrl requires https on public hosts', () => {
  for (const url of [
    'https://example.com',
    'https://host-1.example.org:8443/path'
  ]) {
    assert.equal(isAllowedServerUrl(url), true, url);
  }
  for (const url of [
    'http://example.com',
    'http://8.8.8.8:5080',
    'http://172.32.0.1'
  ]) {
    assert.equal(isAllowedServerUrl(url), false, url);
  }
});

test('GM isAllowedServerUrl allows cleartext only for loopback/private/link-local', () => {
  for (const url of [
    'http://localhost:5080',
    'http://127.0.0.1:5080',
    'http://192.168.1.10/',
    'http://10.0.0.1:5080/x',
    'http://172.16.0.1',
    'http://172.31.255.255',
    'http://169.254.1.1'
  ]) {
    assert.equal(isAllowedServerUrl(url), true, url);
  }
});

test('GM isAllowedServerUrl rejects malformed schemes, userinfo and non-dotted cleartext', () => {
  for (const url of [
    'not a url',
    'ftp://example.com',
    'file:///etc/passwd',
    'https://',
    'http:///',
    'https://user:pass@evil.com',
    'http://user@127.0.0.1',
    'http://127.1',
    'http://0177.0.0.1',
    'http://0x7f000001',
  ]) {
    assert.equal(isAllowedServerUrl(url), false, url);
  }

  // Scheme parsing is case-insensitive: keep this accepted.
  assert.equal(isAllowedServerUrl('HTTPs://EXAMPLE.com'), true);
});

test('writeConfig in both game variants guards the baked address', () => {
  for (const file of [GAME_CONFIG, PC_CONFIG]) {
    const source = fs.readFileSync(file, 'utf8');
    assert.match(source, /MobileConfig\.isValidServerAddress\(server\)/,
      `missing validator call in ${file}`);
    assert.match(source, /isValidServerAddress\(server\)[\s\S]{0,120}throw new Error/,
      `validator result must fail writeConfig in ${file}`);
  }
});
