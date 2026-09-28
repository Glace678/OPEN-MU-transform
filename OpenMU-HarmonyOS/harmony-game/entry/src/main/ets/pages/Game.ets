// Hosts the native SDL surface. The SDL OpenHarmony backend (libSDL3.so) owns
// the XComponent surface/EGL context and spawns SDL_main; all touch input is
// delivered to SDL and turned into gestures by the shared MobileGestureMapper.
// This page only provides the surface, the safe-area overlay, and fold/resize
// awareness (the native side reacts to surface size changes itself).
import router from '@ohos.router';
import { display } from '@kit.ArkUI';

@Entry
@Component
struct Game {
  @State screenWidth: number = 0;
  @State screenHeight: number = 0;
  @State showHelp: boolean = false;

  aboutToAppear(): void {
    const main = display.getDefaultDisplaySync();
    this.screenWidth = main.width;
    this.screenHeight = main.height;
    // Foldable unfold/hover/fold and window resizes: the native renderer remaps
    // the surface itself; we keep the overlay inside the safe area.
    display.on('foldStatusChange', () => {
      const d = display.getDefaultDisplaySync();
      this.screenWidth = d.width;
      this.screenHeight = d.height;
    });
    display.on('change', () => {
      const d = display.getDefaultDisplaySync();
      this.screenWidth = d.width;
      this.screenHeight = d.height;
    });
  }

  aboutToDisappear(): void {
    display.off('foldStatusChange');
    display.off('change');
  }

  private backToSettings(): void {
    router.replaceUrl({ url: 'pages/Bootstrap' });
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
          .onClick(() => this.backToSettings())
      }
      .position({ right: 12, top: 12 })

      if (this.showHelp) {
        this.helpDialog()
      }
    }
    .width('100%').height('100%').backgroundColor(Color.Black)
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
