'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const JSON5 = require('json5');
const { validateMobilePairing } = require('../mobile-pairing.cjs');

const key = 'B'.repeat(43);
const makeProfile = fields => ({ buildOption: { arkOptions: { buildProfileFields: fields } } });

test('JSON5 comments, unquoted names and single quotes cannot bypass the placeholder check', () => {
  const profile = JSON5.parse(`{
    buildOption: { arkOptions: { buildProfileFields: {
      // "MOBILE_PACKAGE_KEY": "${key}",
      MOBILE_PACKAGE_KEY: '${'A'.repeat(43)}', DEFAULT_SERVER_ADDRESS: '192.168.1.2',
    } } }
  }`);
  assert.throws(() => validateMobilePairing(profile, 'game'), /placeholder/);
  assert.doesNotThrow(() => validateMobilePairing(profile, 'game', true));
});

test('missing required keys and addresses are rejected', () => {
  for (const fields of [{}, { MOBILE_PACKAGE_KEY: key }, { DEFAULT_SERVER_ADDRESS: '192.168.1.2' }]) {
    assert.throws(() => validateMobilePairing(makeProfile(fields), 'game'));
  }
  assert.throws(() => validateMobilePairing({}, 'gm'), /Missing/);
});

test('game default addresses use the mobile client private IPv4 policy', () => {
  for (const address of ['localhost', '127.0.0.2', '10.0.2.2', '172.16.1.1', '192.168.1.2', '169.254.1.2']) {
    assert.doesNotThrow(() => validateMobilePairing(makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: address }), 'game'));
  }
  for (const address of ['', 'example.com', '8.8.8.8', '192.168.001.2', '192.168.1.999', '172.32.1.1']) {
    assert.throws(() => validateMobilePairing(makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: address }), 'game'));
  }
});

test('GM validates DEFAULT_SERVER_URL including cleartext public hosts and userinfo', () => {
  for (const address of ['http://localhost:5080', 'http://192.168.1.2:5080', 'https://example.com']) {
    assert.doesNotThrow(() => validateMobilePairing(makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_URL: address }), 'gm'));
  }
  for (const address of ['http://8.8.8.8:5080', 'http://example.com', 'https://', 'http://user@127.0.0.1',
    'http://127.1', 'http://0177.0.0.1', 'http://0x7f000001', 'https://example.com?key=test', 'file:///test']) {
    assert.throws(() => validateMobilePairing(makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_URL: address }), 'gm'));
  }
});

test('release overrides are validated after merging with the base configuration', () => {
  const profile = makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: '10.0.0.2' });
  profile.buildOptionSet = [{ name: 'release', arkOptions: { buildProfileFields: { MOBILE_PACKAGE_KEY: 'A'.repeat(43) } } }];
  assert.throws(() => validateMobilePairing(profile, 'game'), /placeholder/);
});

test('local placeholder opt-in does not disable server address validation', () => {
  assert.throws(() => validateMobilePairing(makeProfile({ MOBILE_PACKAGE_KEY: 'A'.repeat(43), DEFAULT_SERVER_URL: 'http://example.com' }), 'gm', true));
});

test('all shipped profiles require explicit placeholder opt-in', () => {
  for (const project of ['harmony-game', 'harmony-gm', 'harmony-pc']) {
    const profile = JSON5.parse(fs.readFileSync(path.join(__dirname, '..', '..', project, 'entry', 'build-profile.json5'), 'utf8'));
    const kind = project === 'harmony-gm' ? 'gm' : (project === 'harmony-pc' ? 'pc' : 'game');
    assert.throws(() => validateMobilePairing(profile, kind), /placeholder/);
    assert.doesNotThrow(() => validateMobilePairing(profile, kind, true));
  }
});

test('public cloud servers require the explicit OPENMU_ALLOW_PUBLIC_SERVER opt-in', () => {
  const profile = () => makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: '203.0.113.10' });
  assert.throws(() => validateMobilePairing(profile(), 'game'), /private IPv4/);
  const previous = process.env.OPENMU_ALLOW_PUBLIC_SERVER;
  try {
    process.env.OPENMU_ALLOW_PUBLIC_SERVER = '1';
    assert.doesNotThrow(() => validateMobilePairing(profile(), 'game'));
    assert.doesNotThrow(() => validateMobilePairing(profile(), 'pc'));
  } finally {
    if (previous === undefined) delete process.env.OPENMU_ALLOW_PUBLIC_SERVER;
    else process.env.OPENMU_ALLOW_PUBLIC_SERVER = previous;
  }
});

test('pc uses the same private address policy as the game kind', () => {
  assert.doesNotThrow(() => validateMobilePairing(
    makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: '192.168.1.2' }), 'pc'));
  assert.throws(() => validateMobilePairing(
    makeProfile({ MOBILE_PACKAGE_KEY: key, DEFAULT_SERVER_ADDRESS: '8.8.8.8' }), 'pc'));
});

// P2-15 guard: the hand-written SHA-256/HMAC copy and the rawfile copy helper
// exist once per project. They must stay byte-identical -- any silent drift
// would derive a different auto-login account (or a different write path) on
// one of the two builds.
test('mirrored ArkTS files stay byte-identical across the game and pc projects', () => {
  const mirrored = ['mobile/MobileIdentity.ets', 'mobile/RawFileCopy.ets'];
  for (const relative of mirrored) {
    const game = fs.readFileSync(path.join(__dirname, '..', '..', 'harmony-game', 'entry', 'src', 'main', 'ets', ...relative.split('/')));
    const pc = fs.readFileSync(path.join(__dirname, '..', '..', 'harmony-pc', 'entry', 'src', 'main', 'ets', ...relative.split('/')));
    assert.equal(game.toString('utf8'), pc.toString('utf8'), `${relative} drifted between harmony-game and harmony-pc`);
  }
});
