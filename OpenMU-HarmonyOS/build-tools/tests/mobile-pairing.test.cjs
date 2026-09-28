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
    const kind = project === 'harmony-gm' ? 'gm' : 'game';
    assert.throws(() => validateMobilePairing(profile, kind), /placeholder/);
    assert.doesNotThrow(() => validateMobilePairing(profile, kind, true));
  }
});
