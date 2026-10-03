// HarmonyOS PC / 2-in-1 render surface. Keyboard and mouse flow through SDL's
// OpenHarmony backend to the normal desktop input path; touch on convertible
// devices still drives the shared MobileGestureMapper. The window is free and
// resizable; the native renderer re-maps the surface on resize.
import { common } from '@kit.AbilityKit';
import { promptAction } from '@kit.ArkUI';

@Entry
@Component
struct Game {
  @State showExit: boolean = false;

  build() {
    Stack() {
      XComponent({ id: 'OpenMUSurface', type: XComponentType.SURFACE, libraryname: 'SDL3' })
        .width('100%')
        .height('100%')

      // Small top-right control to leave the session.
      Row({ space: 8 }) {
        Button('✕')
          .width(40).height(40).fontSize(16).backgroundColor('rgba(16,20,24,0.55)')
          .onClick(() => this.showExit = true)
      }
      .position({ right: 12, top: 12 })

      if (this.showExit) {
        this.exitDialog()
      }
    }
    .width('100%').height('100%').backgroundColor(Color.Black)
  }

  // Never navigate away from this page: replaceUrl destroys the XComponent
  // while the SDL thread is still rendering on it (black screen / crash on the
  // next visit). Terminating the ability ends the whole session instead, which
  // matches the swipe-away lifecycle of any other app.
  @Builder
  exitDialog() {
    Column() {
      Text('退出游戏').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 8 })
      Text('退出后可在启动页修改设置并重新开始。')
        .fontSize(14).fontColor('#414750')
      Row({ space: 12 }) {
        Button('取消').backgroundColor('#313840')
          .onClick(() => this.showExit = false)
        Button('退出游戏').backgroundColor('#BE3028')
          .onClick(() => {
            this.showExit = false;
            const context = getContext(this) as common.UIAbilityContext;
            context.terminateSelf().catch((e: Object) => {
              promptAction.showToast({ message: '退出失败：' + JSON.stringify(e) });
            });
          })
      }.margin({ top: 16 })
    }
    .width('70%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }
}

export default Game;
