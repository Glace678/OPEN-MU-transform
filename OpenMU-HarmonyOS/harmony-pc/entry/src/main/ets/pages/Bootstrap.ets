import router from '@ohos.router';
import zlib from '@ohos.zlib';
import { fileIo as fs } from '@kit.CoreFileKit';
import { promptAction } from '@kit.ArkUI';
import { MobileConfig, DEFAULT_SERVER_ADDRESS } from '../mobile/MobileConfig';
// Re-apply the data root after first-run extraction (see harmony-game notes).
import bridge from 'libopenmubridge.so';

// HarmonyOS PC / 2-in-1 launcher: windowed, keyboard + mouse primary. No login
// screen — the client auto-logs into the paired local account.
@Entry
@Component
struct Bootstrap {
  @State status: string = '准备游戏数据…';
  @State detail: string = '首次启动会解压游戏数据，请预留约 2 GB 空间。';
  @State busy: boolean = false;
  @State ready: boolean = false;
  @State showSettings: boolean = false;
  @State showHelp: boolean = false;
  @State serverAddress: string = DEFAULT_SERVER_ADDRESS;
  @State fps: string = '60';
  @State renderScale: number = 100;

  async aboutToAppear(): Promise<void> {
    const context = getContext(this);
    this.serverAddress = await MobileConfig.getString(context, 'server-address', DEFAULT_SERVER_ADDRESS);
    this.fps = await MobileConfig.getString(context, 'fps', '60');
    this.renderScale = await MobileConfig.getNumber(context, 'render-scale', 100);
    if (await MobileConfig.isGameDataReady(context)) {
      this.markReady();
    } else {
      await this.prepareData();
    }
  }

  private markReady(): void {
    this.ready = true;
    this.busy = false;
    this.status = '准备完成';
    this.detail = '点击开始游戏，将自动登录本地账号。键鼠操作为主，触屏二合一设备也支持手势。';
  }

  private async prepareData(): Promise<void> {
    if (this.busy) {
      return;
    }
    this.busy = true;
    this.status = '正在解压游戏数据…';
    const context = getContext(this);
    const root = MobileConfig.dataRoot(context);
    try {
      fs.mkdirSync(root, true);
      const zipPath = context.filesDir + '/game-data.zip';
      const bytes = await context.resourceManager.getRawFileContent('game-data.zip');
      const out = fs.openSync(zipPath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE | fs.OpenMode.TRUNC);
      try {
        fs.writeSync(out.fd, bytes.buffer);
      } finally {
        fs.closeSync(out);
      }
      await zlib.decompressFile(zipPath, root, { overwrite: true });
      try {
        fs.unlinkSync(zipPath);
      } catch (e) {
        // best effort
      }
      if (await MobileConfig.isGameDataReady(context)) {
        this.markReady();
      } else {
        this.busy = false;
        this.status = '解压完成但未找到 Data 目录';
      }
    } catch (e) {
      this.busy = false;
      this.status = '解压失败';
      this.detail = '未找到内置数据包。请把 game-data.zip 放入 entry/src/main/resources/rawfile/，'
        + '或用 hdc 把 Data 目录推送到 ' + root + '。';
    }
  }

  private async startGame(): Promise<void> {
    const context = getContext(this);
    try {
      await MobileConfig.writeConfig(context);
      // Data folder now exists; re-point the native process at it (onCreate's
      // setDataRoot ran before first-run extraction and could not chdir there).
      bridge.setDataRoot(MobileConfig.dataRoot(context));
    } catch (e) {
      promptAction.showToast({ message: '写入配置失败：' + ((e as Error).message ?? e) });
      return;
    }
    router.replaceUrl({ url: 'pages/Game' });
  }

  private async saveSettings(): Promise<void> {
    const context = getContext(this);
    await MobileConfig.setString(context, 'server-address', this.serverAddress);
    await MobileConfig.setString(context, 'fps', this.fps);
    await MobileConfig.setNumber(context, 'render-scale', this.renderScale);
    this.showSettings = false;
    promptAction.showToast({ message: '已保存，进入游戏后生效' });
  }

  build() {
    Stack() {
      Column() {
        Text('OpenMU 鸿蒙电脑版').fontSize(22).fontColor('#D84133').margin({ bottom: 14 })
        Text(this.status).fontSize(26).fontColor(Color.White).margin({ bottom: 8 })
        Text(this.detail).fontSize(14).fontColor('#BEC5CC').textAlign(TextAlign.Center)
          .padding({ left: 40, right: 40 })
        if (this.busy) {
          Progress({ type: ProgressType.Linear }).width(420).margin({ top: 20 }).color('#D84133')
        }
        Blank()
        Column({ space: 10 }) {
          Button('开始游戏').width(260).height(52).backgroundColor('#BE3028')
            .enabled(this.ready).onClick(() => this.startGame())
          Button('设置').width(260).height(48).backgroundColor('#313840')
            .onClick(() => this.showSettings = true)
          Button('操作说明').width(260).height(48).backgroundColor('#313840')
            .onClick(() => this.showHelp = true)
          if (!this.ready && !this.busy) {
            Button('重试').width(260).height(48).backgroundColor('#313840')
              .onClick(() => this.prepareData())
          }
        }.margin({ bottom: 36 })
      }
      .width('100%').height('100%').justifyContent(FlexAlign.Center)
      .backgroundColor('#101418').padding(24)

      if (this.showSettings) {
        this.settingsDialog();
      }
      if (this.showHelp) {
        this.helpDialog();
      }
    }.width('100%').height('100%')
  }

  @Builder
  settingsDialog() {
    Column() {
      Text('游戏设置').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 12 })
      Scroll() {
        Column() {
          Text('电脑服务器 IPv4 地址').fontSize(14).fontColor('#414750').width('100%')
          TextInput({ text: this.serverAddress }).height(46).width('100%')
            .onChange((v: string) => this.serverAddress = v)
          Text('帧率（follow/30/45/60/90/120）').fontSize(14).width('100%').margin({ top: 10 })
          TextInput({ text: this.fps }).height(46).width('100%')
            .onChange((v: string) => this.fps = v)
          Text(`渲染比例：${this.renderScale}%`).fontSize(14).width('100%').margin({ top: 10 })
          Slider({ value: this.renderScale, min: 50, max: 100, step: 5 })
            .onChange((v: number) => this.renderScale = Math.round(v))
        }
      }.layoutWeight(1).width('100%')
      Row({ space: 12 }) {
        Button('取消').backgroundColor('#313840').onClick(() => this.showSettings = false)
        Button('保存').backgroundColor('#BE3028').onClick(() => this.saveSettings())
      }.margin({ top: 12 })
    }.width(460).height('70%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  helpDialog() {
    Column() {
      Text('键盘 / 鼠标操作').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 10 })
      Scroll() {
        Column() {
          this.line('鼠标左键', '点击 / 移动 / 选中')
          this.line('鼠标右键', '释放技能 / NPC 交互')
          this.line('鼠标滚轮', '拉近 / 拉远镜头')
          this.line('键盘 1-9 / F1-F8', '技能 / 物品快捷栏')
          this.line('键盘 M / Esc', '地图 / 系统菜单')
          Text('触屏二合一设备同时支持手机手势：左半屏拖动移动、右半屏双击技能、'
            + '双指切换技能/缩放、三指地图/设置。')
            .fontSize(13).fontColor('#5C636C').margin({ top: 10 })
        }.alignItems(HorizontalAlign.Start).width('100%')
      }.layoutWeight(1).width('100%')
      Button('知道了').width('100%').height(48).backgroundColor('#BE3028').margin({ top: 12 })
        .onClick(() => this.showHelp = false)
    }.width(480).height('64%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  line(control: string, action: string) {
    Row() {
      Text(control).fontSize(15).fontWeight(FontWeight.Medium).layoutWeight(1)
      Text(action).fontSize(15).fontColor('#414750')
    }.width('100%').padding({ top: 6, bottom: 6 })
  }
}
