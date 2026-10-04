import { AbilityConstant, UIAbility, Want } from '@kit.AbilityKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { window } from '@kit.ArkUI';
import { MobileConfig } from '../mobile/MobileConfig';
// The NAPI bridge built from cpp/native_bridge.cpp (libopenmubridge.so). It lets
// us configure the process before SDL's OpenHarmony backend starts SDL_main.
import bridge from 'libopenmubridge.so';

const DOMAIN = 0x4f50; // "OP"

// H-05: surfaced when native bridge startup setup throws; Bootstrap refuses to
// route into the game until this is null.
export let nativeStartupError: string | null = null;

export default class EntryAbility extends UIAbility {
  onCreate(want: Want, launchParam: AbilityConstant.LaunchParam): void {
    hilog.info(DOMAIN, 'OpenMU', 'Game ability created');
    try {
      // Point the native game at the extracted data directory and inject the
      // auto-login identity. This mirrors Android GameActivity, which sets the
      // same variables via SDLActivity.nativeSetenv before the SDL thread runs.
      bridge.setDataRoot(MobileConfig.dataRoot(this.context));
      const env = MobileConfig.autoLoginEnv();
      Object.keys(env).forEach((name: string) => {
        bridge.setEnv(name, env[name]);
      });
    } catch (e) {
      // H-05: record the failure instead of silently continuing. native_bridge
      // now throws on bad args / failed setenv/chdir, and Bootstrap will refuse
      // to start the game while nativeStartupError is set.
      nativeStartupError = 'native bridge init failed: ' + JSON.stringify(e)
      hilog.error(DOMAIN, 'OpenMU', 'native bridge init failed: %{public}s', JSON.stringify(e));
    }
  }

  onWindowStageCreate(windowStage: window.WindowStage): void {
    // Immersive, full-screen, landscape game. Orientation is pinned in
    // module.json5; also request landscape + keep-awake at runtime.
    windowStage.getMainWindow().then((win: window.Window) => {
      win.setWindowLayoutFullScreen(true).catch((e: Object) => {
        hilog.warn(DOMAIN, 'OpenMU', 'fullscreen failed: %{public}s', JSON.stringify(e));
      });
      win.setWindowKeepScreenOn(true).catch((e: Object) => {
        hilog.warn(DOMAIN, 'OpenMU', 'keep-screen-on failed: %{public}s', JSON.stringify(e));
      });
      win.setPreferredOrientation(window.Orientation.LANDSCAPE).catch((e: Object) => {
        hilog.warn(DOMAIN, 'OpenMU', 'orientation failed: %{public}s', JSON.stringify(e));
      });
    });

    windowStage.loadContent('pages/Bootstrap', (err) => {
      if (err.code) {
        hilog.error(DOMAIN, 'OpenMU', 'loadContent failed: %{public}s', JSON.stringify(err));
      }
    });
  }

  onDestroy(): void {
  }
}
