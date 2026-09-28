import { promptAction } from '@kit.ArkUI';
import { preferences } from '@kit.ArkData';
import BuildProfile from 'BuildProfile';
import {
  CharacterOption,
  ItemOption,
  MobileGmClient,
  isAllowedServerUrl,
  randomUuid
} from '../model/MobileGmClient';

// Injected by DevEco from entry/build-profile.json5 buildProfileFields (same
// values as the Android Gradle BuildConfig). The package key MUST match the
// game APK and the server pairing key.
const DEFAULT_SERVER_URL: string = BuildProfile.DEFAULT_SERVER_URL;
const MOBILE_PACKAGE_KEY: string = BuildProfile.MOBILE_PACKAGE_KEY;

const PREFS_NAME = 'openmu-gm-harmony';
const KEY_SERVER_URL = 'server-url';

const MAX_ZEN = 2_000_000_000;

@Entry
@Component
struct Index {
  @State serverUrl: string = DEFAULT_SERVER_URL;
  @State editingServer: boolean = false;
  @State connecting: boolean = false;
  @State statusText: string = '正在连接本地服务器…';
  @State serverReady: boolean = false;
  @State accountName: string = '';
  @State characters: CharacterOption[] = [];
  @State selectedCharacterIndex: number = 0;
  @State items: ItemOption[] = [];
  @State selectedItemIndex: number = -1;
  @State searchQuery: string = '';
  @State searchStatus: string = '';
  @State level: number = 0;
  @State maxLevel: number = 0;
  @State quantity: number = 1;
  @State hasSkill: boolean = false;
  @State hasLuck: boolean = false;
  @State additionalLevel: number = 0;
  @State excellentChecked: boolean[] = [];
  @State excellentAll: boolean = false;
  @State grantBusy: boolean = false;
  @State resultText: string = '请先刷新角色并搜索物品。';
  @State zenAmount: string = '1000000';

  private client(): MobileGmClient {
    return new MobileGmClient(this.serverUrl, MOBILE_PACKAGE_KEY);
  }

  async aboutToAppear(): Promise<void> {
    try {
      const store = await preferences.getPreferences(getContext(this), PREFS_NAME);
      const saved = await store.get(KEY_SERVER_URL, DEFAULT_SERVER_URL) as string;
      if (saved && isAllowedServerUrl(saved)) {
        this.serverUrl = saved;
      }
    } catch (e) {
      // Keep the default URL if preferences are unavailable.
    }
    await this.loadStatus();
  }

  private async saveServerUrl(url: string): Promise<void> {
    const store = await preferences.getPreferences(getContext(this), PREFS_NAME);
    await store.put(KEY_SERVER_URL, url);
    await store.flush();
  }

  async loadStatus(): Promise<void> {
    this.connecting = true;
    this.statusText = '正在连接本地服务器…';
    this.serverReady = false;
    this.accountName = '';
    this.characters = [];
    this.selectedCharacterIndex = 0;
    try {
      const status = await this.client().loadStatus();
      this.accountName = status.accountName;
      this.characters = status.characters;
      this.serverReady = status.serverReady;
      this.statusText = status.serverReady
        ? `已连接，游戏服务器运行中：${this.serverUrl}`
        : `管理服务已连接，但游戏服务器未启动：${this.serverUrl}`;
      if (!status.serverReady) {
        this.resultText = '游戏服务器尚未启动，请在电脑上运行 OpenMU-Local 后刷新。';
      } else if (this.characters.length === 0) {
        this.resultText = '当前没有在线角色，请先进入游戏后刷新。';
      }
    } catch (e) {
      this.statusText = `连接失败：${(e as Error).message ?? e}`;
    } finally {
      this.connecting = false;
    }
  }

  async searchItems(): Promise<void> {
    this.searchStatus = '正在搜索物品…';
    try {
      const found = await this.client().searchItems(this.searchQuery);
      this.items = found;
      this.selectedItemIndex = found.length > 0 ? 0 : -1;
      this.applySelectedItem();
      this.searchStatus = found.length === 0 ? '没有找到匹配的物品。' : `找到 ${found.length} 个物品。`;
    } catch (e) {
      this.searchStatus = (e as Error).message ?? '搜索失败';
    }
  }

  private applySelectedItem(): void {
    const item = this.selectedItemIndex >= 0 ? this.items[this.selectedItemIndex] : undefined;
    if (!item) {
      this.maxLevel = 0;
      this.level = 0;
      this.hasSkill = false;
      this.hasLuck = false;
      this.additionalLevel = 0;
      this.excellentChecked = [];
      this.excellentAll = false;
      return;
    }
    this.maxLevel = item.maxLevel;
    this.level = Math.min(this.level, item.maxLevel);
    if (!item.canHaveSkill) {
      this.hasSkill = false;
    }
    if (!item.canHaveLuck) {
      this.hasLuck = false;
    }
    if (!item.canHaveAdditional) {
      this.additionalLevel = 0;
    }
    this.excellentChecked = new Array<boolean>(Math.max(0, Math.min(item.excellentCount, 12))).fill(false);
    this.excellentAll = false;
  }

  private excellentMask(): number {
    let mask = 0;
    for (let i = 0; i < this.excellentChecked.length; i++) {
      if (this.excellentChecked[i]) {
        mask |= (1 << i);
      }
    }
    return mask;
  }

  private selectedCharacter(): CharacterOption | undefined {
    return this.selectedCharacterIndex >= 0 && this.selectedCharacterIndex < this.characters.length
      ? this.characters[this.selectedCharacterIndex]
      : undefined;
  }

  private currentItem(): ItemOption | undefined {
    return this.selectedItemIndex >= 0 && this.selectedItemIndex < this.items.length
      ? this.items[this.selectedItemIndex]
      : undefined;
  }

  private currentMoneyText(): string {
    const character = this.selectedCharacter();
    if (!character || character.money < 0) {
      return '未获取';
    }
    return character.money.toLocaleString();
  }

  async grantItem(): Promise<void> {
    const character = this.selectedCharacter();
    const item = this.currentItem();
    if (this.grantBusy || !character || !item) {
      return;
    }
    this.grantBusy = true;
    this.resultText = '正在发放，请勿重复点击…';
    try {
      const message = await this.client().grantItem({
        requestId: randomUuid(),
        characterId: character.id,
        group: item.group,
        number: item.number,
        level: this.level,
        quantity: this.quantity,
        hasSkill: item.canHaveSkill && this.hasSkill,
        hasLuck: item.canHaveLuck && this.hasLuck,
        additionalOptionLevel: item.canHaveAdditional ? this.additionalLevel : 0,
        excellentMask: this.excellentMask()
      });
      this.resultText = message;
      promptAction.showToast({ message: '装备已发放' });
      await this.loadStatus();
    } catch (e) {
      this.resultText = `操作失败：${(e as Error).message ?? e}`;
    } finally {
      this.grantBusy = false;
    }
  }

  async grantZen(): Promise<void> {
    const character = this.selectedCharacter();
    if (this.grantBusy || !character) {
      return;
    }
    const amount = Number.parseInt(this.zenAmount, 10);
    if (!Number.isFinite(amount) || amount <= 0 || amount > MAX_ZEN) {
      this.resultText = '请输入 1 到 20 亿之间的金币数量。';
      return;
    }
    this.grantBusy = true;
    this.resultText = '正在发放金币，请勿重复点击…';
    try {
      const message = await this.client().grantZen({ requestId: randomUuid(), characterId: character.id, amount });
      this.resultText = message;
      promptAction.showToast({ message: '金币已发放' });
      await this.loadStatus();
    } catch (e) {
      this.resultText = `操作失败：${(e as Error).message ?? e}`;
    } finally {
      this.grantBusy = false;
    }
  }

  @Builder
  sectionTitle(text: string) {
    Text(text)
      .fontSize(17)
      .fontWeight(FontWeight.Bold)
      .fontColor('#20242A')
      .margin({ top: 18, bottom: 6 })
      .width('100%')
  }

  @Builder
  label(text: string) {
    Text(text)
      .fontSize(15)
      .fontColor('#414750')
      .margin({ top: 8, bottom: 4 })
      .width('100%')
  }

  @Builder
  checkRow(isOn: boolean, enabled: boolean, text: string, onToggle: (on: boolean) => void) {
    Row() {
      Toggle({ type: ToggleType.Checkbox, isOn: isOn })
        .enabled(enabled)
        .onChange((on: boolean) => onToggle(on))
      Text(text).fontSize(16).margin({ left: 8 })
    }
    .width('100%')
    .height(44)
  }

  @Builder
  zenPresetButton(label: string, amount: number) {
    Button(label)
      .layoutWeight(1).height(44).fontSize(13).margin({ right: 6 })
      .onClick(() => {
        this.zenAmount = `${amount}`;
      })
  }

  build() {
    Column() {
      Row() {
        Text('OpenMU 鸿蒙 GM')
          .fontSize(18).fontWeight(FontWeight.Bold).fontColor(Color.White)
          .layoutWeight(1)
        Button('刷新')
          .type(ButtonType.Capsule).backgroundColor('#313840').fontColor(Color.White)
          .margin({ right: 8 }).enabled(!this.connecting)
          .onClick(() => this.loadStatus())
        Button('服务器')
          .type(ButtonType.Capsule).backgroundColor('#313840').fontColor(Color.White)
          .onClick(() => {
            this.editingServer = !this.editingServer;
          })
      }
      .width('100%').height(56).padding({ left: 16, right: 8 })
      .backgroundColor('#15191E')

      if (this.connecting) {
        Progress({ type: ProgressType.Linear }).width('100%').height(3).color('#D84133')
      }

      Scroll() {
        GridRow({
          columns: { sm: 4, md: 8, lg: 12 },
          gutter: { x: 16, y: 8 },
          breakpoints: { value: ['600vp', '840vp'] }
        }) {
          // Center and cap the form width on tablets, foldables and 2-in-1.
          GridCol({ span: { sm: 4, md: 8, lg: 8 }, offset: { sm: 0, md: 0, lg: 2 } }) {
            this.bodyColumn()
          }
        }
        .width('100%')
        .padding({ left: 16, right: 16, bottom: 32 })
      }
      .layoutWeight(1)
      .scrollBar(BarState.Auto)
    }
    .width('100%').height('100%').backgroundColor('#F6F7F9')
  }

  @Builder
  bodyColumn() {
    Column() {
      Text(this.statusText).fontSize(14).fontColor('#5C636C').margin({ top: 12 }).width('100%')

      if (this.editingServer) {
        Row() {
          TextInput({ text: this.serverUrl })
            .layoutWeight(1).height(44)
            .onChange((v: string) => {
              this.serverUrl = v;
            })
          Button('连接').height(44).margin({ left: 8 })
            .onClick(async () => {
              if (!isAllowedServerUrl(this.serverUrl)) {
                this.resultText = 'HTTP 仅允许本机或局域网 IP；公网必须用 HTTPS';
                return;
              }
              await this.saveServerUrl(this.serverUrl);
              this.editingServer = false;
              await this.loadStatus();
            })
        }.width('100%').margin({ top: 8 })
      }

      this.sectionTitle('游戏账号与在线角色')
      Text(this.accountName ? `本地账号：${this.accountName}` : '账号：尚未获取')
        .fontSize(16).fontColor('#20242A').height(40).width('100%')
      this.label('选择在线角色')
      Select(this.characters.map((c: CharacterOption): SelectOption => {
        const money = c.money >= 0 ? c.money.toLocaleString() : '—';
        const level = c.level > 0 ? `  (Lv ${c.level})` : '';
        return { value: `${c.name}${level}    金币 ${money}` } as SelectOption;
      }))
        .selected(this.selectedCharacterIndex)
        .width('100%').height(48)
        .onSelect((index: number) => {
          this.selectedCharacterIndex = index;
        })

      this.sectionTitle('发放装备 / 道具')
      TextInput({ placeholder: '输入物品名称或编号', text: this.searchQuery })
        .height(48).width('100%')
        .onChange((v: string) => {
          this.searchQuery = v;
        })
      Button('搜索物品').width('100%').height(48).margin({ top: 8 })
        .onClick(() => this.searchItems())
      Text(this.searchStatus).fontSize(13).fontColor('#5C636C').margin({ top: 6 }).width('100%')

      this.label('选择物品')
      Select(this.items.map((it: ItemOption): SelectOption => {
        return { value: `${it.name}  (${it.group}:${it.number})` } as SelectOption;
      }))
        .selected(this.selectedItemIndex)
        .width('100%').height(48)
        .onSelect((index: number) => {
          this.selectedItemIndex = index;
          this.applySelectedItem();
        })

      this.label(`物品等级：${this.level} / ${this.maxLevel}`)
      Slider({ value: this.level, min: 0, max: Math.max(1, this.maxLevel), step: 1 })
        .width('100%')
        .onChange((v: number) => {
          this.level = Math.round(v);
        })

      this.label('数量')
      Select([1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map((n: number): SelectOption => {
        return { value: `${n}` } as SelectOption;
      }))
        .selected(this.quantity - 1)
        .width('100%').height(48)
        .onSelect((index: number) => {
          this.quantity = index + 1;
        })

      this.checkRow(this.hasSkill, this.currentItem()?.canHaveSkill ?? false, '附带技能', (on: boolean) => {
        this.hasSkill = on;
      })
      this.checkRow(this.hasLuck, this.currentItem()?.canHaveLuck ?? false, '幸运（Luck）', (on: boolean) => {
        this.hasLuck = on;
      })

      this.label('追加属性（+4 / +8 / +12 / +16）')
      Select(['不追加', '+4', '+8', '+12', '+16'].map((s: string): SelectOption => {
        return { value: s } as SelectOption;
      }))
        .selected(this.additionalLevel)
        .enabled(this.currentItem()?.canHaveAdditional ?? false)
        .width('100%').height(48)
        .onSelect((index: number) => {
          this.additionalLevel = index;
        })

      if (this.excellentChecked.length > 0) {
        this.label('卓越属性')
        this.checkRow(this.excellentAll, true, '全选卓越', (on: boolean) => {
          this.excellentAll = on;
          this.excellentChecked = this.excellentChecked.map(() => on);
        })
        ForEach(this.excellentChecked, (checked: boolean, index: number) => {
          Row() {
            Toggle({ type: ToggleType.Checkbox, isOn: checked })
              .onChange((on: boolean) => {
                const next = this.excellentChecked.slice();
                next[index] = on;
                this.excellentChecked = next;
              })
            Text(`卓越 ${index + 1}`).fontSize(16).margin({ left: 8 })
          }.width('100%').height(44)
        }, (_: boolean, index: number) => `ex-${index}`)
      } else {
        Text('该物品无卓越属性').fontSize(14).fontColor('#8C929A').height(40).width('100%')
      }

      Button('发放装备/道具到所选角色')
        .width('100%').height(52).margin({ top: 16 }).backgroundColor('#BE3028')
        .enabled(!this.grantBusy && this.characters.length > 0 && this.items.length > 0)
        .onClick(() => this.grantItem())

      Text(this.resultText).fontSize(14).fontColor('#20242A').margin({ top: 10 }).width('100%')

      this.sectionTitle('发放金币（Zen）')
      Text(`当前余额：${this.currentMoneyText()}`)
        .fontSize(15).fontColor('#414750').height(40).width('100%')
      TextInput({ text: this.zenAmount, placeholder: `1 到 ${MAX_ZEN.toLocaleString()}` })
        .type(InputType.Number).height(48).width('100%')
        .onChange((v: string) => {
          this.zenAmount = v;
        })
      Row() {
        this.zenPresetButton('100 万', 1_000_000)
        this.zenPresetButton('1000 万', 10_000_000)
        this.zenPresetButton('1 亿', 100_000_000)
        this.zenPresetButton('满额', MAX_ZEN)
      }.width('100%').margin({ top: 6 })
      Button('发放金币到所选角色')
        .width('100%').height(52).margin({ top: 10 }).backgroundColor('#BE3028')
        .enabled(!this.grantBusy && this.characters.length > 0)
        .onClick(() => this.grantZen())
    }
    .width('100%')
    .alignItems(HorizontalAlign.Start)
  }
}
