// Hosts the native SDL surface. The SDL OpenHarmony backend (libSDL3.so) owns
// the XComponent surface/EGL context and spawns SDL_main; all touch input is
// delivered to SDL and turned into gestures by the shared MobileGestureMapper.
// This page only provides the surface, the safe-area overlay, and fold/resize
// awareness (the native side reacts to surface size changes itself).
import { common } from '@kit.AbilityKit';
import router from '@ohos.router';
import { display } from '@kit.ArkUI';
import { promptAction } from '@kit.ArkUI';

@Entry
@Component
struct Game {
  @State screenWidth: number = 0;
  @State screenHeight: number = 0;
  @State showHelp: boolean = false;
  @State showSettings: boolean = false;

  // Kept as members so aboutToDisappear can unregister exactly these listeners
  // instead of stripping every listener for the event name.
  private foldCallback = (): void => {
    const d = display.getDefaultDisplaySync();
    this.screenWidth = d.width;
    this.screenHeight = d.height;
  };

  private changeCallback = (): void => {
    const d = display.getDefaultDisplaySync();
    this.screenWidth = d.width;
    this.screenHeight = d.height;
  };

  aboutToAppear(): void {
    const main = display.getDefaultDisplaySync();
    this.screenWidth = main.width;
    this.screenHeight = main.height;
    // Foldable unfold/hover/fold and window resizes: the native renderer remaps
    // the surface itself; we keep the overlay inside the safe area.
    display.on('foldStatusChange', this.foldCallback);
    display.on('change', this.changeCallback);
  }

  aboutToDisappear(): void {
    display.off('foldStatusChange', this.foldCallback);
    display.off('change', this.changeCallback);
  }

  // Never navigate away from this page: replaceUrl destroys the XComponent
  // while the SDL thread is still rendering on it (black screen / crash on the
  // next visit). Settings live on the Bootstrap launcher page and only apply at
  // game start, so the in-game button explains that and offers a clean exit
  // (terminating the ability takes the whole session down with it, which is the
  // same lifecycle as swiping the app away).
  private openSettings(): void {
    this.showSettings = true;
  }

  private leaveGame(): void {
    this.showSettings = false;
    const context = getContext(this) as common.UIAbilityContext;
    context.terminateSelf().catch((e: Object) => {
      promptAction.showToast({ message: '退出失败：' + JSON.stringify(e) });
    });
  }

  build() {
    Stack() {
      // SDL hooks this surface via its OpenHarmony napi module (libraryname).
      XComponent({ id: 'OpenMUSurface', type: XComponentType.SURFACE, libraryname: 'SDL3' })
        .width('100%')
        .height('100%')

      // Top-right utility buttons, kept inside the safe area.
      Row({ space: 8 }) {
        Button('?')
          .width(44).height(44).fontSize(20).backgroundColor('rgba(16,20,24,0.55)')
          .onClick(() => {
            this.showHelp = true;
          })
        Button('⚙')
          .width(44).height(44).fontSize(18).backgroundColor('rgba(16,20,24,0.55)')
          .onClick(() => this.openSettings())
      }
      .position({ right: 12, top: 12 })

      if (this.showHelp) {
        this.helpDialog()
      }
      if (this.showSettings) {
        this.settingsDialog()
      }
    }
    .width('100%').height('100%').backgroundColor(Color.Black)
  }

  @Builder
  settingsDialog() {
    Column() {
      Text('游戏设置').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 12 })
      Text('服务器地址、帧率、渲染比例与手势设置都在启动页的「设置」里，')
        .fontSize(14).fontColor('#414750').margin({ bottom: 6 })
      Text('修改后需要退出并重新进入游戏才会生效。游戏中切换页面会中断渲染，')
        .fontSize(14).fontColor('#414750').margin({ bottom: 6 })
      Text('所以这里只提供继续游戏或完整退出。')
        .fontSize(14).fontColor('#414750')
      Row({ space: 12 }) {
        Button('继续游戏').backgroundColor('#313840')
          .onClick(() => this.showSettings = false)
        Button('退出游戏').backgroundColor('#BE3028')
          .onClick(() => this.leaveGame())
      }.margin({ top: 16 })
    }
    .width('78%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  helpDialog() {
    Column() {
      Text('手机手势操作').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 10 })
      Scroll() {
        Column() {
          this.line('左半屏单指拖动', '移动角色')
          this.line('右半屏单击', '选中 / 确认')
          this.line('右半屏双击', '释放当前技能')
          this.line('双指左右滑', '切换技能')
          this.line('双指上下滑 / 捏合', '拉近 / 拉远镜头')
          this.line('三指上滑', '打开地图')
          this.line('三指下滑', '打开游戏设置')
        }.alignItems(HorizontalAlign.Start).width('100%')
      }.layoutWeight(1).width('100%')
      Button('继续游戏').width('100%').height(48).backgroundColor('#BE3028').margin({ top: 12 })
        .onClick(() => {
          this.showHelp = false;
        })
    }
    .width('70%').height('66%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  line(gesture: string, action: string) {
    Row() {
      Text(gesture).fontSize(15).fontWeight(FontWeight.Medium).layoutWeight(1)
      Text(action).fontSize(15).fontColor('#414750')
    }.width('100%').padding({ top: 6, bottom: 6 })
  }
}
