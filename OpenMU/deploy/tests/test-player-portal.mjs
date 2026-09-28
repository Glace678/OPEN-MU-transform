// Requires Node, existing Playwright, PowerShell, and an optional Caddy binary.
// Only synthetic account responses and random localhost ports are used.
import assert from 'node:assert/strict';
import { createServer, request as httpRequest } from 'node:http';
import { execFileSync, spawn } from 'node:child_process';
import { readFile, writeFile, mkdtemp, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const deploy = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = path.resolve(deploy, '../src/Web/AdminPanel');
const root = path.join(source, 'wwwroot/player-portal');
const packageRoot = process.argv[2];
const caddy = process.argv[3];
assert.ok(packageRoot, 'Pass the directory containing existing Playwright node_modules.');
const load = createRequire(path.join(path.resolve(packageRoot), 'package.json'));
const { chromium } = load('playwright');
const ps = process.platform === 'win32' ? 'powershell.exe' : 'pwsh';
const resourcePath = path.join(source, 'Properties').replaceAll("'", "''");
const script = `$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $all=@{}; `
  + `Get-ChildItem -LiteralPath '${resourcePath}' -Filter 'SelfServiceResources*.resx' | ForEach-Object { `
  + `$culture=$_.BaseName.Replace('SelfServiceResources','').TrimStart('.'); if (-not $culture) { $culture='en' }; `
  + `[xml]$xml=Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8; $strings=@{}; `
  + `foreach($item in $xml.root.data) { $strings[[string]$item.name]=[string]$item.value }; $all[$culture]=$strings }; `
  + `$all | ConvertTo-Json -Depth 5 -Compress`;
const translations = JSON.parse(execFileSync(ps, ['-NoProfile', '-EncodedCommand', Buffer.from(script, 'utf16le').toString('base64')], { encoding: 'utf8', windowsHide: true }));
const posts = [];
const upstreamRequests = [];
const code = Array(8).fill('A1B2C3D4').join('-');
function probeRequest(url, { headers, method = 'GET', body } = {}) {
  return new Promise((resolve, reject) => {
    // Unlike Fetch, node:http preserves an explicit Host header for this virtual-host probe.
    const request = httpRequest(url, { headers, method }, response => {
      response.resume();
      response.on('end', () => resolve({ status: response.statusCode, headers: new Headers(response.headers) }));
      response.on('error', reject);
    });
    request.on('error', reject);
    request.end(body);
  });
}
const messages = { create: 'RegistrationSuccess', 'change-password': 'PasswordChangeSuccess', 'reset-password': 'PasswordResetSuccess', 'recovery-code': 'RecoveryIssueSuccess' };
const server = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://localhost');
  const culture = url.searchParams.get('culture') || 'en';
  const text = translations[culture] || translations.en;
  if (url.pathname.startsWith('/api/')) {
    upstreamRequests.push({ method: request.method, path: url.pathname, headers: request.headers });
    response.setHeader('Content-Type', 'application/json');
    response.setHeader('Cache-Control', 'no-store');
    if (request.method === 'GET' && url.pathname === '/api/registration/text') return response.end(JSON.stringify(text));
    const chunks = [];
    let payload;
    try {
      for await (const chunk of request) chunks.push(chunk);
      payload = JSON.parse(Buffer.concat(chunks).toString());
    } catch {
      response.writeHead(400);
      return response.end('{}');
    }
    posts.push({ path: url.pathname, payload, headers: request.headers });
    const endpoint = url.pathname.split('/').pop();
    const recoveryCode = endpoint === 'change-password' ? undefined : code;
    return response.end(JSON.stringify({ success: true, code: 'ok', message: text[messages[endpoint]], recoveryCode }));
  }
  const names = { '/': 'index.html', '/register': 'index.html', '/change-password': 'index.html', '/reset-password': 'index.html', '/portal.css': 'portal.css', '/portal.js': 'portal.js', '/inventory_back.png': 'inventory_back.png' };
  const name = names[url.pathname];
  if (!name) { response.writeHead(404); return response.end('Not found'); }
  const types = { '.html': 'text/html', '.css': 'text/css', '.js': 'application/javascript', '.png': 'image/png' };
  response.setHeader('Content-Type', types[path.extname(name)]);
  return response.end(await readFile(path.join(root, name)));
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const base = `http://127.0.0.1:${server.address().port}`;
let browser;
let worker;
let temporary;
let checks = 0;
let proxyLog = '';
try {
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  await context.addCookies([{ name: 'test-admin-session', value: 'synthetic', url: base }]);
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(base + '/register?culture=zh-CN');
  await page.locator('form[data-endpoint="create"] button').waitFor({ state: 'visible' });
  await page.waitForFunction(() => !document.querySelector('button[type="submit"]').disabled);
  await page.locator('#register-name').fill('solotest');
  await page.locator('#register-password').fill('old-password');
  await page.locator('#register-confirm').fill('old-password');
  await page.locator('#register-security').fill('123456');
  await page.locator('form[data-endpoint="create"] button').click();
  await page.locator('#result').waitFor({ state: 'visible' });
  assert.equal(await page.locator('#recovery-result').inputValue(), code);
  assert.equal(await page.locator('form[data-endpoint="create"]').isVisible(), false);
  assert.equal(posts[0].payload.securityCode, '123456');
  assert.equal(posts[0].headers.cookie, undefined);
  checks += 4;
  await page.locator('[data-view="reset-password"]').click();
  await page.locator('#reset-name').fill('solotest');
  await page.locator('#reset-new').fill('new-password');
  await page.locator('#reset-confirm').fill('new-password');
  const before = posts.length;
  await page.locator('form[data-endpoint="reset-password"] button').click();
  assert.equal(posts.length, before, 'No recovery code must mean no API submission.');
  await page.locator('#reset-code').fill(code.toLowerCase());
  await page.locator('form[data-endpoint="reset-password"] button').click();
  await page.waitForFunction(() => document.querySelector('#reset-code').value === '');
  assert.equal(posts.at(-1).payload.recoveryCode, code.toLowerCase());
  assert.equal(await page.locator('#reset-new').inputValue(), '');
  checks += 3;
  await page.locator('summary').click();
  await page.locator('#issue-name').fill('solotest');
  await page.locator('#issue-password').fill('current-password');
  await page.locator('form[data-endpoint="recovery-code"] button').click();
  await page.waitForFunction(() => document.querySelector('#issue-password').value === '');
  assert.equal(posts.at(-1).payload.currentPassword, 'current-password');
  checks++;
  await page.locator('[data-view="change-password"]').click();
  await page.locator('#change-name').fill('solotest');
  await page.locator('#change-current').fill('current-password');
  await page.locator('#change-new').fill('new-password');
  await page.locator('#change-confirm').fill('different-password');
  const changeBefore = posts.length;
  await page.locator('form[data-endpoint="change-password"] button').click();
  assert.equal(posts.length, changeBefore);
  await page.locator('#change-confirm').fill('new-password');
  await page.locator('form[data-endpoint="change-password"] button').click();
  await page.waitForFunction(() => document.querySelector('#change-current').value === '');
  assert.equal(posts.at(-1).payload.oldPassword, 'current-password');
  assert.equal(await page.evaluate(() => localStorage.length + sessionStorage.length), 0);
  checks += 3;
  for (const viewport of [{ width: 1280, height: 800 }, { width: 390, height: 844 }, { width: 320, height: 740 }]) {
    await page.setViewportSize(viewport);
    await page.goto(base + '/register?culture=en');
    await page.waitForFunction(() => !document.querySelector('button[type="submit"]').disabled);
    for (const culture of Object.keys(translations)) {
      await page.locator('#culture').selectOption(culture);
      await page.waitForFunction(language => document.documentElement.lang === language && !document.querySelector('button[type="submit"]').disabled, culture);
      for (const view of ['register', 'change-password', 'reset-password']) {
        await page.locator(`[data-view="${view}"]`).click();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, `${culture}:${view}:${viewport.width}`);
        const overflow = await page.locator('nav button').evaluateAll(elements => elements.some(element => element.scrollWidth > element.clientWidth));
        assert.equal(overflow, false, `Tab text overflow: ${culture}:${viewport.width}`);
        checks += 2;
      }
    }
  }
  await page.locator('[data-view="register"]').focus();
  await page.keyboard.press('ArrowRight');
  assert.equal(await page.locator('[data-view="change-password"]').getAttribute('aria-selected'), 'true');
  assert.deepEqual(errors, []);
  checks += 2;
  assert.equal(await page.evaluate(() => new Promise(resolve => {
    const image = new Image();
    image.onload = () => resolve(image.naturalWidth > 0 && image.naturalHeight > 0);
    image.onerror = () => resolve(false);
    image.src = '/inventory_back.png';
  })), true);
  checks++;
  await page.locator('#culture').selectOption('zh-CN');
  await page.waitForFunction(() => document.documentElement.lang === 'zh-CN' && !document.querySelector('button[type="submit"]').disabled);
  await page.locator('[data-view="reset-password"]').click();
  const screenshot = path.join(tmpdir(), 'openmu-player-portal-mobile.png');
  await page.screenshot({ path: screenshot, fullPage: true });
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.locator('[data-view="register"]').click();
  await page.screenshot({ path: path.join(tmpdir(), 'openmu-player-portal-desktop.png'), fullPage: true });
  console.log(`Browser: ${checks} checks passed; screenshot ${screenshot}`);

  if (caddy) {
    temporary = await mkdtemp(path.join(tmpdir(), 'openmu-portal-proxy-test-'));
    const configuration = JSON.parse(execFileSync(caddy, ['adapt', '--config', path.join(deploy, 'all-in-one/Caddyfile.portal'), '--adapter', 'caddyfile'], {
      encoding: 'utf8', windowsHide: true, env: { ...process.env, OPENMU_PORTAL_DOMAIN: 'accounts.example.com' },
    }));
    assert.equal(configuration.admin.disabled, true);
    assert.deepEqual(configuration.apps.http.servers.srv0.listen, [':443']);
    assert.equal(configuration.apps.tls.automation.policies[0].issuers[0].challenges.http.disabled, true);
    const probe = createServer();
    await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
    const port = probe.address().port;
    await new Promise(resolve => probe.close(resolve));
    const runtime = configuration.apps.http.servers.srv0;
    runtime.listen = [`127.0.0.1:${port}`];
    runtime.automatic_https = { disable: true };
    delete configuration.apps.tls;
    const replaceProbeValues = value => {
      if (!value || typeof value !== 'object') return;
      if (value.handler === 'reverse_proxy') value.upstreams = [{ dial: `127.0.0.1:${server.address().port}` }];
      if (value.handler === 'vars' && value.root) value.root = root;
      Object.values(value).forEach(replaceProbeValues);
    };
    replaceProbeValues(configuration);
    const configFile = path.join(temporary, 'probe.json');
    await writeFile(configFile, JSON.stringify(configuration));
    worker = spawn(caddy, ['run', '--config', configFile], { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    worker.stdout.on('data', value => { proxyLog += value; });
    worker.stderr.on('data', value => { proxyLog += value; });
    const proxy = `http://127.0.0.1:${port}`;
    const headers = { Host: 'accounts.example.com', Cookie: 'test=synthetic', Authorization: 'Bearer synthetic' };
    let running = false;
    for (let attempt = 0; attempt < 50; attempt++) {
      try { if ((await probeRequest(proxy + '/register', { headers })).status === 200) { running = true; break; } } catch { /* Own temporary worker is still starting. */ }
      await new Promise(resolve => setTimeout(resolve, 100));
    }
    assert.ok(running, proxyLog);
    let proxyChecks = 3;
    for (const route of ['/login', '/auth/login', '/auth/register', '/accounts', '/api/accounts', '/api/mobile-gm/status', '/logs', '/_blazor', '/_framework/blazor.server.js', '/api/registration/create/', '/api/registration/create%2f..%2faccounts']) {
      const beforeRequests = upstreamRequests.length;
      assert.equal((await probeRequest(proxy + route, { headers })).status, 404, route);
      assert.equal((await probeRequest(proxy + route, { headers, method: 'POST', body: '{}' })).status, 404, route);
      assert.equal(upstreamRequests.length, beforeRequests, route + ' must not reach the upstream.');
      proxyChecks += 3;
    }
    for (const route of ['/create', '/change-password', '/reset-password', '/recovery-code']) {
      const response = await probeRequest(proxy + '/api/registration' + route, { method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' }, body: '{}' });
      assert.equal(response.status, 200);
      assert.equal(upstreamRequests.at(-1).headers.cookie, undefined);
      assert.equal(upstreamRequests.at(-1).headers.authorization, undefined);
      assert.equal(response.headers.get('cache-control'), 'no-store');
      proxyChecks += 4;
    }
    assert.equal((await probeRequest(proxy + '/api/registration/text?culture=zh-CN', { headers })).status, 200);
    assert.equal((await probeRequest(proxy + '/api/registration/reset-password', { headers })).status, 404);
    assert.equal((await probeRequest(proxy + '/api/registration/text', { headers, method: 'POST', body: '{}' })).status, 404);
    assert.equal((await probeRequest(proxy + '/api/registration/create', { headers, method: 'POST', body: 'x'.repeat(9000) })).status, 413);
    assert.equal((await probeRequest(proxy + '/portal.js', { headers })).status, 200);
    assert.equal((await probeRequest(proxy + '/inventory_back.png', { headers })).status, 200);
    proxyChecks += 6;
    console.log(`Caddy: ${proxyChecks} checks passed (TLS disabled only for the local route probe).`);
  }
} finally {
  await browser?.close();
  if (worker && worker.exitCode === null) {
    const stopped = new Promise(resolve => worker.once('exit', resolve));
    worker.kill();
    await stopped;
  }
  await new Promise(resolve => server.close(resolve));
  if (temporary) {
    const target = path.resolve(temporary);
    assert.equal(path.dirname(target), path.resolve(tmpdir()), 'Cleanup must stay inside the temporary parent.');
    assert.ok(path.basename(target).startsWith('openmu-portal-proxy-test-'), 'Refuse cleanup of an unexpected directory.');
    await rm(target, { recursive: true, force: true });
  }
}
