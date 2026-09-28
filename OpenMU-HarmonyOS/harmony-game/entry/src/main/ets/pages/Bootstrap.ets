import router from '@ohos.router';
import zlib from '@ohos.zlib';
import { fileIo as fs } from '@kit.CoreFileKit';
import { promptAction } from '@kit.ArkUI';
import { MobileConfig, DEFAULT_SERVER_ADDRESS, GAME_DATA_VERSION } from '../mobile/MobileConfig';
// NAPI bridge (libopenmubridge.so). EntryAbility.onCreate already called
// setDataRoot before the data archive existed, so on a first run the chdir and
// MU_CONFIG_FILE setup failed; we re-apply the data root here once it exists.
import bridge from 'libopenmubridge.so';

// Prepares the game data (extracting the bundled rawfile archive on first run),
// mirrors the Android BootstrapActivity: no login screen — the game auto-logs
// into the paired local account.
@Entry
@Component
struct Bootstrap {
  @State status: string = '准备游戏数据…';
  @State detail: string = '首次启动会在应用内解压游戏数据，请预留约 2 GB 空间。';
  @State busy: boolean = false;
  @State ready: boolean = false;
  @State showSettings: boolean = false;
  @State showTutorial: boolean = false;
  @State serverAddress: string = DEFAULT_SERVER_ADDRESS;
  @State fps: string = '60';
  @State renderScale: number = 100;
  @State uiScale: number = 100;
  @State handedness: string = 'right';

  async aboutToAppear(): Promise<void> {
    const context = getContext(this);
    this.serverAddress = await MobileConfig.getString(context, 'server-address', DEFAULT_SERVER_ADDRESS);
    this.fps = await MobileConfig.getString(context, 'fps', '60');
    this.renderScale = await MobileConfig.getNumber(context, 'render-scale', 100);
    this.uiScale = await MobileConfig.getNumber(context, 'ui-scale', 100);
    this.handedness = await MobileConfig.getString(context, 'handedness', 'right');
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
    this.detail = '点击开始游戏，将自动登录本地账号。';
  }

  // Copies the bundled rawfile/game-data.zip into the sandbox and unzips it.
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
      await this.copyRawfileTo(context, 'game-data.zip', zipPath);
      await zlib.decompressFile(zipPath, root, { overwrite: true });
      try {
        fs.unlinkSync(zipPath);
      } catch (e) {
        // Best effort cleanup.
      }
      if (await MobileConfig.isGameDataReady(context)) {
        this.markReady();
      } else {
        this.busy = false;
        this.status = '解压完成但未找到 Data 目录';
        this.detail = '请确认 game-data.zip 内含 Data 文件夹。';
      }
    } catch (e) {
      this.busy = false;
      this.status = '解压失败';
      // If no archive was bundled, tell the user how to provide the data.
      this.detail = '未找到内置数据包。请把 game-data.zip 放入 entry/src/main/resources/rawfile/，'
        + '或用 hdc 把 Data 目录推送到 ' + root + '。';
    }
  }

  // Copies a bundled rawfile into the sandbox. The game archive is large; the
  // supported cross-SDK path is to read it via resourceManager and write once.
  private async copyRawfileTo(context: Context, rawName: string, destPath: string): Promise<void> {
    const bytes = await context.resourceManager.getRawFileContent(rawName);
    const out = fs.openSync(destPath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE | fs.OpenMode.TRUNC);
    try {
      fs.writeSync(out.fd, bytes.buffer);
    } finally {
      fs.closeSync(out);
    }
  }

  private async startGame(): Promise<void> {
    const context = getContext(this);
    try {
      await MobileConfig.writeConfig(context);
      // The extracted data folder now definitely exists (writeConfig also
      // mkdirs it), so re-point the native process at it. onCreate's earlier
      // setDataRoot ran before first-run extraction and could not chdir there.
      bridge.setDataRoot(MobileConfig.dataRoot(context));
    } catch (e) {
      promptAction.showToast({ message: '写入配置失败：' + ((e as Error).message ?? e) });
      return;
    }
    if (!(await MobileConfig.tutorialSeen(context))) {
      this.showTutorial = true;
      return;
    }
    router.replaceUrl({ url: 'pages/Game' });
  }

  private async saveSettings(): Promise<void> {
    const context = getContext(this);
    await MobileConfig.setString(context, 'server-address', this.serverAddress);
    await MobileConfig.setString(context, 'fps', this.fps);
    await MobileConfig.setNumber(context, 'render-scale', this.renderScale);
    await MobileConfig.setNumber(context, 'ui-scale', this.uiScale);
    await MobileConfig.setString(context, 'handedness', this.handedness);
    this.showSettings = false;
    promptAction.showToast({ message: '已保存，进入游戏后生效' });
  }

  build() {
    Stack() {
      Column() {
        Text('OpenMU 鸿蒙版').fontSize(20).fontColor('#D84133').margin({ bottom: 12 })
        Text(this.status).fontSize(26).fontColor(Color.White).margin({ bottom: 8 })
        Text(this.detail).fontSize(14).fontColor('#BEC5CC').textAlign(TextAlign.Center)
          .padding({ left: 24, right: 24 })
        if (this.busy) {
          Progress({ type: ProgressType.Linear }).width('60%').margin({ top: 20 }).color('#D84133')
        }
        Blank()
        Column({ space: 10 }) {
          Button('开始游戏').width(220).height(52).backgroundColor('#BE3028')
            .enabled(this.ready).onClick(() => this.startGame())
          Button('设置').width(220).height(48).backgroundColor('#313840')
            .onClick(() => this.showSettings = true)
          Button('手势说明').width(220).height(48).backgroundColor('#313840')
            .onClick(() => this.showTutorial = true)
          if (!this.ready && !this.busy) {
            Button('重试').width(220).height(48).backgroundColor('#313840')
              .onClick(() => this.prepareData())
          }
        }.margin({ bottom: 30 })
      }
      .width('100%').height('100%').justifyContent(FlexAlign.Center)
      .backgroundColor('#101418')
      .padding({ left: 24, right: 24 })

      if (this.showSettings) {
        this.settingsDialog()
      }
      if (this.showTutorial) {
        this.tutorialDialog()
      }
    }
    .width('100%').height('100%')
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
          Text('帧率（follow/30/45/60/90/120）').fontSize(14).fontColor('#414750')
            .width('100%').margin({ top: 10 })
          TextInput({ text: this.fps }).height(46).width('100%')
            .onChange((v: string) => this.fps = v)
          Text(`渲染比例：${this.renderScale}%`).fontSize(14).width('100%').margin({ top: 10 })
          Slider({ value: this.renderScale, min: 50, max: 100, step: 5 })
            .onChange((v: number) => this.renderScale = Math.round(v))
          Text(`按钮大小：${this.uiScale}%`).fontSize(14).width('100%').margin({ top: 10 })
          Slider({ value: this.uiScale, min: 80, max: 140, step: 10 })
            .onChange((v: number) => this.uiScale = Math.round(v))
          Row() {
            Radio({ value: 'right', group: 'hand' }).checked(this.handedness === 'right')
              .onChange(() => this.handedness = 'right')
            Text('右手模式').margin({ right: 20 })
            Radio({ value: 'left', group: 'hand' }).checked(this.handedness === 'left')
              .onChange(() => this.handedness = 'left')
            Text('左手模式')
          }.width('100%').margin({ top: 8 })
        }
      }.layoutWeight(1).width('100%')
      Row({ space: 12 }) {
        Button('取消').backgroundColor('#313840').onClick(() => this.showSettings = false)
        Button('保存').backgroundColor('#BE3028').onClick(() => this.saveSettings())
      }.margin({ top: 12 })
    }
    .width('86%').height('78%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  tutorialDialog() {
    Column() {
      Text('手机手势操作').fontSize(20).fontWeight(FontWeight.Bold).margin({ bottom: 10 })
      Scroll() {
        Column() {
          this.gestureLine('左半屏单指拖动', '移动角色')
          this.gestureLine('右半屏单击', '选中 / 确认')
          this.gestureLine('右半屏双击', '释放当前技能')
          this.gestureLine('双指左右滑', '切换技能')
          this.gestureLine('双指上下滑 / 捏合', '拉近 / 拉远镜头')
          this.gestureLine('三指上滑', '打开地图')
          this.gestureLine('三指下滑', '打开游戏设置')
          Text('左手模式只交换移动区与技能区。')
            .fontSize(13).fontColor('#5C636C').margin({ top: 10 })
        }.alignItems(HorizontalAlign.Start)
      }.layoutWeight(1).width('100%')
      Button('知道了').width('100%').height(48).backgroundColor('#BE3028').margin({ top: 12 })
        .onClick(async () => {
          this.showTutorial = false;
          const context = getContext(this);
          // Write the shared constant, not a copy of it: tutorialSeen()
          // compares against GAME_DATA_VERSION, so a literal here would keep
          // showing (or never show) the tutorial after the constant is bumped.
          await MobileConfig.setString(context, 'tutorial-version', GAME_DATA_VERSION);
          // If opened from the start button, proceed into the game.
          if (this.ready) {
            router.replaceUrl({ url: 'pages/Game' });
          }
        })
    }
    .width('86%').height('72%').backgroundColor(Color.White).borderRadius(14).padding(20)
  }

  @Builder
  gestureLine(gesture: string, action: string) {
    Row() {
      Text(gesture).fontSize(15).fontWeight(FontWeight.Medium).layoutWeight(1)
      Text(action).fontSize(15).fontColor('#414750')
    }.width('100%').padding({ top: 6, bottom: 6 })
  }
}
