import { AbilityConstant, UIAbility, Want } from '@kit.AbilityKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { window } from '@kit.ArkUI';
import { MobileConfig } from '../mobile/MobileConfig';
import bridge from 'libopenmubridge.so';

const DOMAIN = 0x4f50; // "OP"

export default class EntryAbility extends UIAbility {
  onCreate(want: Want, launchParam: AbilityConstant.LaunchParam): void {
    hilog.info(DOMAIN, 'OpenMU-PC', 'PC ability created');
    try {
      // Inject auto-login identity + data root before SDL_main starts, same as
      // the phone build.
      bridge.setDataRoot(MobileConfig.dataRoot(this.context));
      const env = MobileConfig.autoLoginEnv();
      Object.keys(env).forEach((name: string) => {
        bridge.setEnv(name, env[name]);
      });
    } catch (e) {
      hilog.error(DOMAIN, 'OpenMU-PC', 'native bridge init failed: %{public}s', JSON.stringify(e));
    }
  }

  onWindowStageCreate(windowStage: window.WindowStage): void {
    windowStage.getMainWindow().then((win: window.Window) => {
      // Desktop: a normal resizable window, defaulting to a 4:3-ish play area.
      win.setWindowKeepScreenOn(true);
      win.resize(1280, 800).catch((e: Object) => {
        hilog.warn(DOMAIN, 'OpenMU-PC', 'resize failed: %{public}s', JSON.stringify(e));
      });
      // Keep a visible mouse cursor for keyboard + mouse play.
      try {
        win.setWindowLayoutFullScreen(false);
      } catch (e) {
        // Not all window modes accept this; ignore.
      }
    });

    windowStage.loadContent('pages/Bootstrap', (err) => {
      if (err.code) {
        hilog.error(DOMAIN, 'OpenMU-PC', 'loadContent failed: %{public}s', JSON.stringify(err));
      }
    });
  }

  onDestroy(): void {
  }
}
