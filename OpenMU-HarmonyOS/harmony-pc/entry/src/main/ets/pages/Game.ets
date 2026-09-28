// HarmonyOS PC / 2-in-1 render surface. Keyboard and mouse flow through SDL's
// OpenHarmony backend to the normal desktop input path; touch on convertible
// devices still drives the shared MobileGestureMapper. The window is free and
// resizable; the native renderer re-maps the surface on resize.
import router from '@ohos.router';

@Entry
@Component
struct Game {
  build() {
    Stack() {
      XComponent({ id: 'OpenMUSurface', type: XComponentType.SURFACE, libraryname: 'SDL3' })
        .width('100%')
        .height('100%')

      // Small top-right control to leave the session.
      Row({ space: 8 }) {
        Button('✕')
          .width(40).height(40).fontSize(16).backgroundColor('rgba(16,20,24,0.55)')
          .onClick(() => router.replaceUrl({ url: 'pages/Bootstrap' }))
      }
      .position({ right: 12, top: 12 })
    }
    .width('100%').height('100%').backgroundColor(Color.Black)
  }
}

export default Game;
