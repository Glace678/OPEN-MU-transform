// ArkTS port of the Android GM API client. It talks to the same
// /api/mobile-gm/* endpoints on the local OpenMU server and carries the same
// X-OpenMU-Mobile-Key pairing header, so the server-side account and grants
// are identical across Android and HarmonyOS.
import http from '@ohos.net.http';

export interface CharacterOption {
  id: string;
  name: string;
  level: number;
  money: number; // -1 when the server did not report a balance
}

export interface ItemOption {
  group: number;
  number: number;
  name: string;
  maxLevel: number;
  canHaveSkill: boolean;
  canHaveLuck: boolean;
  canHaveAdditional: boolean;
  excellentCount: number;
}

export interface StatusResult {
  accountName: string;
  // Whether a game server is started (the AdminPanel host can answer while the
  // game world is still offline).
  serverReady: boolean;
  characters: CharacterOption[];
}

export class ApiError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

function trimSlash(value: string): string {
  let result = (value ?? '').trim();
  while (result.endsWith('/')) {
    result = result.substring(0, result.length - 1);
  }
  return result;
}

function clean(value: string | undefined | null): string {
  return (value ?? '').trim();
}

// Only allow loopback, RFC1918 private and link-local IPv4 over plain HTTP,
// mirroring ServerAddressPolicy on Android. Public hosts must be HTTPS.
// Parsed manually (no global URL dependency) for stable behaviour across SDKs.
export function isAllowedServerUrl(rawUrl: string): boolean {
  const value = (rawUrl ?? '').trim();
  const protocolMatch = value.match(/^([a-zA-Z][a-zA-Z0-9+.-]*):\/\//);
  if (!protocolMatch) {
    return false;
  }
  const protocol = protocolMatch[1].toLowerCase();
  if (protocol === 'https') {
    return true;
  }
  if (protocol !== 'http') {
    return false;
  }
  const rest = value.substring(protocolMatch[0].length);
  const hostPart = rest.split('/')[0].split('?')[0].split('#')[0];
  // Strip userinfo and port.
  const host = hostPart.split('@').pop()!.split(':')[0];
  if (host === 'localhost' || host === '127.0.0.1') {
    return true;
  }
  const parts = host.split('.');
  if (parts.length !== 4) {
    return false;
  }
  const octets: number[] = [];
  for (const part of parts) {
    if (!/^\d+$/.test(part)) {
      return false;
    }
    const octet = Number(part);
    if (octet < 0 || octet > 255) {
      return false;
    }
    octets.push(octet);
  }
  const a = octets[0];
  const b = octets[1];
  // 10/8, 172.16/12, 192.168/16, 169.254/16 link-local.
  return a === 10
    || (a === 172 && b >= 16 && b <= 31)
    || (a === 192 && b === 168)
    || (a === 169 && b === 254);
}

interface RawResponse {
  code: number;
  body: string;
}

export class MobileGmClient {
  private baseUrl: string;
  private packageKey: string;

  constructor(baseUrl: string, packageKey: string) {
    this.baseUrl = trimSlash(baseUrl);
    this.packageKey = packageKey;
  }

  private async send(method: string, path: string, body?: string): Promise<RawResponse> {
    const request = http.createHttp();
    try {
      const headers: Record<string, string> = {
        'Accept': 'application/json',
        'X-OpenMU-Mobile-Key': this.packageKey
      };
      if (body !== undefined) {
        headers['Content-Type'] = 'application/json; charset=utf-8';
      }
      const response = await request.request(this.baseUrl + '/api/mobile-gm' + path, {
        method: method === 'GET' ? http.RequestMethod.GET : http.RequestMethod.POST,
        header: headers,
        extraData: body,
        connectTimeout: 7000,
        readTimeout: 10000
      });
      const text = typeof response.result === 'string'
        ? response.result
        : new TextDecoder().decode(response.result as ArrayBuffer);
      return { code: response.responseCode as number, body: text };
    } catch (e) {
      throw new ApiError('无法连接服务器：' + ((e as Error).message ?? '网络错误'));
    } finally {
      request.destroy();
    }
  }

  private async requestJson(method: string, path: string, body?: string): Promise<Object | null> {
    const response = await this.send(method, path, body);
    if (response.code < 200 || response.code >= 300) {
      const message = this.extractMessage(response.body);
      throw new ApiError('服务器返回 HTTP ' + response.code + (message ? '：' + message : ''));
    }
    if (response.body.trim().length === 0) {
      return {};
    }
    try {
      return JSON.parse(response.body) as Object;
    } catch (e) {
      throw new ApiError('服务器返回了无效 JSON');
    }
  }

  private extractMessage(body: string): string {
    try {
      const parsed = JSON.parse(body) as Record<string, Object>;
      return clean(parsed['message'] as string ?? parsed['error'] as string);
    } catch (e) {
      return '';
    }
  }

  private async unwrapGrant(method: string, path: string, body: string, defaultMessage: string): Promise<string> {
    const root = await this.requestJson(method, path, body) as Record<string, Object> | null;
    let message = defaultMessage;
    if (root) {
      const returned = clean(root['message'] as string);
      if ('success' in root && !root['success']) {
        throw new ApiError(returned.length > 0 ? returned : '服务器拒绝了操作');
      }
      if (returned.length > 0) {
        message = returned;
      }
    }
    return message;
  }

  async loadStatus(): Promise<StatusResult> {
    const root = await this.requestJson('GET', '/status') as Record<string, Object>;
    let accountName = clean(root['accountName'] as string);
    if (accountName.length === 0) {
      const localAccount = root['localAccount'];
      if (localAccount && typeof localAccount === 'object') {
        const account = localAccount as Record<string, string>;
        accountName = clean(account['loginName'] ?? account['name']);
      } else if (typeof localAccount === 'string') {
        accountName = clean(localAccount);
      }
    }
    if (accountName.length === 0) {
      accountName = clean((root['account'] as string) ?? (root['loginName'] as string));
    }

    let array = root['characters'] as Object[] | undefined;
    if (!array) {
      array = root['onlineCharacters'] as Object[] | undefined;
    }
    if (!array) {
      throw new ApiError('服务器状态响应缺少在线角色列表');
    }

    const characters: CharacterOption[] = [];
    for (const entry of array) {
      const character = entry as Record<string, Object>;
      const id = clean((character['characterId'] as string) ?? (character['id'] as string));
      const name = clean((character['name'] as string) ?? (character['characterName'] as string));
      if (id.length > 0 && name.length > 0) {
        characters.push({
          id,
          name,
          level: (character['level'] as number) ?? 0,
          money: (character['money'] as number) ?? -1
        });
      }
    }
    return { accountName, serverReady: root['serverReady'] === true, characters };
  }

  async searchItems(query: string): Promise<ItemOption[]> {
    const encoded = encodeURIComponent((query ?? '').trim());
    const root = await this.requestJson('GET', '/items?q=' + encoded);
    let array: Object[];
    if (Array.isArray(root)) {
      array = root as Object[];
    } else {
      array = ((root as Record<string, Object>)['items'] as Object[]) ?? [];
    }
    const items: ItemOption[] = [];
    for (const entry of array) {
      const item = entry as Record<string, Object>;
      const group = item['group'] as number;
      const number = item['number'] as number;
      if (group === undefined || number === undefined) {
        continue;
      }
      const name = clean((item['name'] as string) ?? (item['displayName'] as string));
      if (name.length === 0) {
        continue;
      }
      items.push({
        group,
        number,
        name,
        maxLevel: Math.max(0, Math.min(255, (item['maxLevel'] as number) ?? 15)),
        canHaveSkill: (item['hasSkill'] as boolean) ?? false,
        canHaveLuck: (item['canHaveLuck'] as boolean) ?? false,
        canHaveAdditional: (item['canHaveAdditionalOption'] as boolean)
          ?? (item['canHaveAdditional'] as boolean) ?? false,
        excellentCount: Math.max(0, Math.min(31,
          (item['excellentOptionCount'] as number) ?? (item['excellentCount'] as number) ?? 0))
      });
    }
    return items;
  }

  async grantItem(request: {
    requestId: string; characterId: string; group: number; number: number;
    level: number; quantity: number; hasSkill: boolean; hasLuck: boolean;
    additionalOptionLevel: number; excellentMask: number;
  }): Promise<string> {
    const body = JSON.stringify(request);
    return this.unwrapGrant('POST', '/grant-item', body, '物品已成功发放');
  }

  async grantZen(request: { requestId: string; characterId: string; amount: number }): Promise<string> {
    const body = JSON.stringify(request);
    return this.unwrapGrant('POST', '/grant-zen', body, '金币已成功发放');
  }
}

export function randomUuid(): string {
  // RFC4122 version 4 UUID. The idempotency key only needs to be unique per
  // button press, so Math.random is sufficient here.
  const bytes = new Uint8Array(16);
  for (let i = 0; i < bytes.length; i++) {
    bytes[i] = Math.floor(Math.random() * 256);
  }
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex: string[] = [];
  for (let i = 0; i < bytes.length; i++) {
    hex.push(bytes[i].toString(16).padStart(2, '0'));
  }
  return `${hex.slice(0, 4).join('')}-${hex.slice(4, 6).join('')}-${hex.slice(6, 8).join('')}-${hex.slice(8, 10).join('')}-${hex.slice(10, 16).join('')}`;
}
