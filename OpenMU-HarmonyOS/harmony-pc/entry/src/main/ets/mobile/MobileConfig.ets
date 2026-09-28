// Mirrors Android MobilePreferences.java for the HarmonyOS PC / 2-in-1 build.
import { preferences } from '@kit.ArkData';
import { fileIo as fs } from '@kit.CoreFileKit';
import { display } from '@kit.ArkUI';
import BuildProfile from 'BuildProfile';
import { deriveCredentials } from './MobileIdentity';

// BuildConfig-style constants injected by DevEco from entry/build-profile.json5
// buildProfileFields (same values as the Android Gradle BuildConfig). The
// pairing key MUST match the GM app and the server.
export const MOBILE_PACKAGE_KEY: string = BuildProfile.MOBILE_PACKAGE_KEY;
export const DEFAULT_SERVER_ADDRESS: string = BuildProfile.DEFAULT_SERVER_ADDRESS;
export const GAME_PORT = 44406;

const PREFS = 'openmu-game-pc';
const KEY_SERVER = 'server-address';
const KEY_FPS = 'fps';
const KEY_RENDER_SCALE = 'render-scale';
const KEY_UI_SCALE = 'ui-scale';
const KEY_SAFE_MARGIN = 'safe-margin';
const KEY_HANDEDNESS = 'handedness';
const KEY_TUTORIAL_VERSION = 'tutorial-version';
export const GAME_DATA_VERSION = 'harmony-pc-1';

export class MobileConfig {
  static async store(context: Context): Promise<preferences.Preferences> {
    return preferences.getPreferences(context, PREFS);
  }

  static async getString(context: Context, key: string, fallback: string): Promise<string> {
    const store = await MobileConfig.store(context);
    return (await store.get(key, fallback)) as string;
  }

  static async getNumber(context: Context, key: string, fallback: number): Promise<number> {
    const store = await MobileConfig.store(context);
    return (await store.get(key, fallback)) as number;
  }

  static async setString(context: Context, key: string, value: string): Promise<void> {
    const store = await MobileConfig.store(context);
    await store.put(key, value);
    await store.flush();
  }

  static async setNumber(context: Context, key: string, value: number): Promise<void> {
    const store = await MobileConfig.store(context);
    await store.put(key, value);
    await store.flush();
  }

  static dataRoot(context: Context): string {
    return context.filesDir + '/game-data';
  }

  static async isGameDataReady(context: Context): Promise<boolean> {
    const root = MobileConfig.dataRoot(context);
    try {
      fs.accessSync(root + '/Data');
      return true;
    } catch (e) {
      return false;
    }
  }

  // Writes the config.ini consumed by the native GameConfig parser. Mirrors
  // Android MobilePreferences.writeConfig: the same section/key names
  // ([CONNECTION SETTINGS]/ServerIP, [Render]/FrameRateMode, [Input]/Handedness,
  // ...), merged into the shipped config instead of replacing it so the
  // [LOGIN]/[Audio] defaults from the data archive survive.
  static async writeConfig(context: Context): Promise<void> {
    const root = MobileConfig.dataRoot(context);
    fs.mkdirSync(root, true);

    const server = (await MobileConfig.getString(context, KEY_SERVER, DEFAULT_SERVER_ADDRESS))
      .replace(/[\r\n[\]=]/g, '');
    const renderScale = await MobileConfig.getNumber(context, KEY_RENDER_SCALE, 100);
    const uiScale = await MobileConfig.getNumber(context, KEY_UI_SCALE, 100);
    const safeMargin = await MobileConfig.getNumber(context, KEY_SAFE_MARGIN, 3);
    const handedness = await MobileConfig.getString(context, KEY_HANDEDNESS, 'right');
    const fps = await MobileConfig.getString(context, KEY_FPS, '60');
    const followDisplay = fps === 'follow';
    const fixedFps = followDisplay ? 60 : (Number.parseInt(fps, 10) || 60);

    // Landscape pixel size of the active display (like Android realMetrics).
    let width = 1280;
    let height = 720;
    try {
      const d = display.getDefaultDisplaySync();
      width = Math.max(d.width, d.height);
      height = Math.min(d.width, d.height);
    } catch (e) {
      // Keep conservative fallbacks if the display service is unavailable.
    }

    const updates = new Map<string, Map<string, string>>();
    putIni(updates, 'Window', 'Width', width.toString());
    putIni(updates, 'Window', 'Height', height.toString());
    putIni(updates, 'Window', 'Windowed', '0');
    putIni(updates, 'CONNECTION SETTINGS', 'ServerIP', server);
    putIni(updates, 'CONNECTION SETTINGS', 'ServerPort', GAME_PORT.toString());
    putIni(updates, 'UI', 'Locale', 'zh-CN');
    putIni(updates, 'UI', 'ScalePercent', uiScale.toString());
    putIni(updates, 'UI', 'SafeMarginPercent', safeMargin.toString());
    putIni(updates, 'Render', 'FrameRateMode', followDisplay ? 'FollowDisplay' : 'Fixed');
    putIni(updates, 'Render', 'FrameRateLimit', fixedFps.toString());
    putIni(updates, 'Render', 'FrameRateLimitMilliHz', (fixedFps * 1000).toString());
    putIni(updates, 'Render', 'BackgroundFrameRate', '30');
    putIni(updates, 'Render', 'VSync', followDisplay ? '1' : '0');
    putIni(updates, 'Render', 'CoreProfile', '0');
    putIni(updates, 'Render', 'RenderScalePercent', renderScale.toString());
    putIni(updates, 'Render', 'EffectQuality', 'balanced');
    putIni(updates, 'Render', 'RenderLevel', '3');
    putIni(updates, 'Render', 'RenderAllEffects', '1');
    putIni(updates, 'Input', 'GamepadEnabled', '1');
    putIni(updates, 'Input', 'MobileTouchEnabled', '1');
    putIni(updates, 'Input', 'TouchOpacityPercent', '65');
    putIni(updates, 'Input', 'Handedness', handedness);
    putIni(updates, 'Haptics', 'Enabled', '1');

    const configPath = root + '/config.ini';
    let existing = '';
    try {
      const f = fs.openSync(configPath, fs.OpenMode.READ_ONLY);
      try {
        existing = fs.readTextSync(f.fd);
      } finally {
        fs.closeSync(f);
      }
    } catch (e) {
      existing = DEFAULT_CONFIG_INI;
    }

    // Write to a temp file and rename so a crash mid-write can't leave a
    // truncated config.ini (same atomic swap as Android).
    const merged = mergeIni(existing, updates);
    const pendingPath = root + '/config.ini.pending';
    const out = fs.openSync(pendingPath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE | fs.OpenMode.TRUNC);
    try {
      fs.writeSync(out.fd, merged);
    } finally {
      fs.closeSync(out);
    }
    fs.renameSync(pendingPath, configPath);
  }

  static autoLoginEnv(): Record<string, string> {
    const credentials = deriveCredentials(MOBILE_PACKAGE_KEY);
    return {
      'MU_LOCAL_AUTO_LOGIN': '1',
      'MU_MOBILE_LOCAL_AUTO_LOGIN': '1',
      'MU_LOCAL_GAME_USERNAME': credentials.username,
      'MU_LOCAL_GAME_PASSWORD': credentials.password
    };
  }

  static async tutorialSeen(context: Context): Promise<boolean> {
    const seen = await MobileConfig.getString(context, KEY_TUTORIAL_VERSION, '');
    return seen === GAME_DATA_VERSION;
  }
}

// Minimal config used when the data archive ships no config.ini yet. Mirrors
// Android MobilePreferences.defaultConfig.
const DEFAULT_CONFIG_INI =
  '[LOGIN]\nRememberMe=0\nSavePassword=0\nLanguage=Eng\n'
  + 'EncryptedUsername=\nEncryptedPassword=\n'
  + '[Audio]\nSoundVolume=5\nMusicVolume=5\n';

function putIni(updates: Map<string, Map<string, string>>, section: string,
  key: string, value: string): void {
  let sectionMap = updates.get(section);
  if (!sectionMap) {
    sectionMap = new Map<string, string>();
    updates.set(section, sectionMap);
  }
  sectionMap.set(key, value);
}

// Case-insensitively replaces the given section/key values in an existing INI
// file, appending any missing keys/sections. Mirrors Android
// MobilePreferences.mergeIni: the phone settings only override the keys they
// own, and everything else shipped in config.ini stays intact.
function mergeIni(source: string, updates: Map<string, Map<string, string>>): string {
  const lines = source.replace(/\r\n/g, '\n').split('\n');

  const canonicalSection = new Map<string, string>();
  const canonicalKeys = new Map<string, Map<string, string>>();
  updates.forEach((keys: Map<string, string>, section: string) => {
    const sectionLower = section.toLowerCase();
    canonicalSection.set(sectionLower, section);
    const keyMap = new Map<string, string>();
    keys.forEach((_v: string, key: string) => keyMap.set(key.toLowerCase(), key));
    canonicalKeys.set(sectionLower, keyMap);
  });

  const written = new Map<string, Set<string>>();
  const seenSections = new Set<string>();
  updates.forEach((_keys: Map<string, string>, section: string) => {
    written.set(section.toLowerCase(), new Set<string>());
  });

  const out: string[] = [];
  let current = '';

  const appendMissing = (sectionLower: string): void => {
    const canonical = canonicalSection.get(sectionLower);
    if (!canonical) {
      return;
    }
    const done = written.get(sectionLower);
    if (!done) {
      return;
    }
    updates.get(canonical)?.forEach((value: string, key: string) => {
      const keyLower = key.toLowerCase();
      if (!done.has(keyLower)) {
        out.push(key + '=' + value);
        done.add(keyLower);
      }
    });
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('[') && trimmed.endsWith(']') && trimmed.length > 2) {
      appendMissing(current);
      current = trimmed.substring(1, trimmed.length - 1).trim().toLowerCase();
      seenSections.add(current);
      out.push(line);
      continue;
    }
    const separator = trimmed.indexOf('=');
    const keys = canonicalKeys.get(current);
    if (separator <= 0 || !keys) {
      out.push(line);
      continue;
    }
    const keyLower = trimmed.substring(0, separator).trim().toLowerCase();
    const canonicalKey = keys.get(keyLower);
    if (!canonicalKey) {
      out.push(line);
      continue;
    }
    const canonical = canonicalSection.get(current);
    const value = canonical ? (updates.get(canonical)?.get(canonicalKey) ?? '') : '';
    out.push(canonicalKey + '=' + value);
    written.get(current)?.add(keyLower);
  }
  appendMissing(current);

  updates.forEach((_keys: Map<string, string>, section: string) => {
    const sectionLower = section.toLowerCase();
    if (seenSections.has(sectionLower)) {
      return;
    }
    out.push('[' + section + ']');
    appendMissing(sectionLower);
  });

  return out.join('\n');
}
