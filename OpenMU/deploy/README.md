# Deployment

The deployment guides moved to the documentation website:

* [Deployment overview](../docs-website/docs/deployment/overview.md) — which
  variant to choose, with its pros and cons
* [All-in-one](../docs-website/docs/deployment/all-in-one.md) — the recommended
  variant; the compose files are in [all-in-one](all-in-one)
* [All-in-one with Traefik](../docs-website/docs/deployment/all-in-one-traefik.md)
  — when you host your website on the same machine; the compose files are in
  [all-in-one-traefik](all-in-one-traefik)
* [Distributed](../docs-website/docs/deployment/distributed.md) — currently
  broken and unsupported; the compose files are in [distributed](distributed)
* [Startup parameters and environment variables](../docs-website/docs/deployment/startup-parameters.md)

## 一键启动本地改造版本

先安装 Docker Desktop（Windows/macOS）或 Docker Engine + Compose 插件（Linux）。
在当前 OpenMU 目录执行以下命令。默认构建整个本地 `src`，包含未提交的本地修改，
使用独立镜像 `openmu-selfhosted:local`，不会使用缺少改造的上游 OpenMU 镜像，也不会推送 GitHub。

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\quick-start.ps1
```

```bash
bash deploy/quick-start.sh
```

第一次启动会生成 `deploy/all-in-one/.env`，保存随机管理员和数据库密码。
查看该文件中的 `OPENMU_ADMIN_USER`、`OPENMU_ADMIN_PASSWORD` 后，在本机打开
`http://127.0.0.1/` 登录。后续执行保留原凭据、数据库卷和存档，不会自动重建数据库。
首次构建仍需联网下载 .NET 基础镜像、NuGet 包、PostgreSQL 和 nginx 镜像；这不上传源码到 GitHub。

默认只允许本机访问游戏端口和管理网页。先预览执行计划（不调用 Docker、不写文件）：

```powershell
.\deploy\quick-start.ps1 -DryRun | ConvertTo-Json -Depth 5
```

```bash
bash deploy/quick-start.sh --dry-run
```

## 手机和电脑连接同一个服务器

将完整 OpenMU 目录放到云服务器上，执行以下命令，把示例地址替换为所有设备都能访问的
服务器公网 IPv4；局域网部署则使用电脑的局域网 IPv4。不要填写 `127.0.0.1`、容器 IP 或带端口的 URL。

```powershell
.\deploy\quick-start.ps1 -PublicAddress 203.0.113.25
```

```bash
bash deploy/quick-start.sh --public-address 203.0.113.25
```

参数会同时保存 `RESOLVE_IP`（发给客户端的服务器地址）和 `OPENMU_GAME_BIND=0.0.0.0`
（主机发布游戏端口）。容器内部的 `OPENMU_BIND_ADDRESS` 固定为 `0.0.0.0`，不能使用主机公网 IP。
已有部署再次执行时保留这两个设置；换公网 IP 时再次传入参数即可，不改变存档和密码。

云防火墙需要允许 TCP `44405`、`44406`、`55901-55906`、`55980`。
全新 Season 6 数据库中，MuMain 开源客户端连接端口为 `44406`，传统 GMO 客户端为 `44405`；
旧数据库中自定义的端口以后台配置为准，需要同步修改 Compose 映射和云防火墙。

手机和电脑客户端都填写同一个服务器 IP 和对应连接端口，游戏进度统一保存在服务器 PostgreSQL
的 `dbdata` 卷中。不同平台切换设备时使用同一账号；多人同时协作使用不同账号。
这不是“分别启动多个单机服再自动合并存档”，也不将现有本机存档自动迁移到云端。
现有 `C:\OpenMU-Local` 的数据库不会被此脚本读取、替换或同步。

## 管理和数据安全

管理网页保持仅 `127.0.0.1` 可访问，数据库 `5432` 不发布到主机。
云服务器管理可以在自己的电脑执行 SSH 隧道，然后打开 `http://127.0.0.1:8088/`：

```bash
ssh -L 8088:127.0.0.1:80 your-user@your-server
```

玩家账号自助使用下面的独立 HTTPS 门户，后台仍只走 SSH 隧道。
不要公开后台的 Blazor 连接、管理登录或全部 `/api`。原有 `docker-compose.prod.yml` /
Traefik 流程是完整后台站点，不是本项目的玩家门户，不要与门户覆盖文件混用。
没有恢复码的匿名重置默认关闭；不要开启 `AccountSelfService:AllowLocalPasswordReset`。
即使显式开启，本机维护也拒绝任何代理请求头，不能用来绕过账号所有权验证。

`DB_HOST`、`DB_ADMIN_USER`、`DB_ADMIN_PW` 实际进入 OpenMU 持久化连接配置，
并与 PostgreSQL 初始化使用相同的管理员用户名和密码。连接配置固定使用 `openmu` 数据库，
修改 `DB_NAME` 为其他名称会被脚本拒绝，而不会造成数据库已建好却连不上的假成功。
管理后台使用独立 `admin` schema，`OPENMU_ADMIN_USER` / `OPENMU_ADMIN_PASSWORD` 在第一次启动前即生效。
脚本会暂时清除可能覆盖 `.env` 的同名 shell 环境变量，执行后恢复调用者环境。

默认数据库镜像固定为 `postgres:18-alpine`，避免无意跨大版本升级。
复用之前部署的 Docker 数据卷前，必须确认原 PostgreSQL 大版本；若原版本不同，在 `.env` 中
将 `POSTGRES_IMAGE` 固定到原版本并完成兼容的卷目录配置。不要直接把旧卷交给新大版本。
升级数据库和迁移本机存档需要先备份，本脚本不执行 `pg_upgrade`、`pg_restore` 或数据卷删除。

`.env` 被 Git 忽略，Unix 下文件权限为 `600`。不要把它放入公开目录或发送给他人。
不要手动改变已有数据库密码；PostgreSQL 对已有卷不会因 `POSTGRES_PASSWORD` 改变而自动改密码。
脚本保留已有凭据，缺失或重复的配置项会直接报错。

## 常用选项和验证

| PowerShell | Bash | 用途 |
|---|---|---|
| `-DryRun` | `--dry-run` | 只读预览，不要求 Docker 已安装 |
| `-PublicAddress IP` | `--public-address IP` | 允许手机/电脑连接并指定广告地址 |
| `-PortalDomain DNS` | `--portal-domain DNS` | 独立玩家 HTTPS 门户，后台不公开 |
| `-WebPort 8088` | `--web-port 8088` | 修改本机管理网页端口 |
| `-ConfigDirectory DIR` | `--config-dir DIR` | 将凭据保存在指定目录 |
| `-Down` | `--down` | 停止 Compose 容器但保留数据库卷 |
| `-Logs` | `--logs` | 跟踪服务启动日志 |
| `-Build` | `--build` | 兼容旧命令，默认就会构建本地源码 |
| `-PublishedImage` | `--published-image` | 显式使用不包含本地改造的上游版本，不推荐 |
| `-NoPull` | `--no-pull` | 只在上游镜像模式下禁止拉取镜像 |

下列测试模拟 Docker，不启动服务、不访问任何数据库；Python 测试需要 PyYAML：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\tests\Test-QuickStart.ps1
python deploy/tests/test-compose.py -v
```

```bash
bash deploy/tests/test-quick-start.sh
```

## 公网玩家账号门户

准备一个指向服务器公网 IPv4 的 DNS 域名，云防火墙允许 TCP `443`。
443 必须空闲；门户只用 TLS-ALPN 自动证书验证，不占用本机管理站点的 80 端口。
公网环境必须使用有效 HTTPS，不能把示例域名当成实际可签发域名。

```powershell
.\deploy\quick-start.ps1 -PublicAddress 203.0.113.25 -PortalDomain accounts.your-domain.com
```

```bash
bash deploy/quick-start.sh --public-address 203.0.113.25 --portal-domain accounts.your-domain.com
```

玩家访问 `https://accounts.your-domain.com/register`、`/change-password`、`/reset-password`。
页面提供 15 种语言，可在地址追加 `?culture=zh-CN`。
`OPENMU_PORTAL_DOMAIN` 会保存到 `.env`，后续启动和 `--down` 自动使用同一门户配置。
门户与本地源码绑定，不允许组合 `--published-image`。证书保存在独立 Docker 卷中。

门户是静态页面，不使用 Blazor、管理员 cookie 或管理员 API。代理只允许四个账号 POST
接口和一个公开文本 GET 接口，其余路径统一 404；上游请求移除 Cookie 和 Authorization。
接口返回 `Cache-Control: no-store`，密码和恢复码不写入浏览器本地存储。
部署前仍应按云供应商能力开启 DDoS/WAF 与请求限流，尤其是匿名注册和验证当前密码的接口。
本项目没有配置供应商专属 WAF，也没有替代服务器安全更新或数据库备份。

本机页面路径为
`/_content/MUnique.OpenMU.Web.AdminPanel/player-portal/index.html?view=reset-password&culture=zh-CN`。
局域网 HTTP 仅适用于可信私有网络，且需已有的局域网网页监听或配对代理；默认 Compose
管理站点仍只监听 localhost，不能把“存在静态页面”当成手机已可直接访问后台。

## 恢复码与旧账号

注册成功会返回 32 字节随机恢复码，显示为 8 组 8 位十六进制字符，用连字符分隔。
数据库只存储 SHA-256 哈希，不存明文；不要把恢复码和账号密码一起公开。
忘记密码必须同时提交账号、恢复码和新密码。成功后轮换恢复码，旧码不能再次使用；
并发恢复通过数据库原子条件更新保证最多一个成功。此功能不是邮件验证或邮箱找回。

旧账号没有自动生成或回填恢复码。知道当前密码的玩家可在找回页面展开“签发或替换恢复码”，
验证后补发；若密码和恢复码都丢失，只能由已认证管理员在后台重置密码。
管理员重置或显式本机维护会撤销旧恢复码，玩家用新密码重新签发。
角色删除用的旧 `SecurityCode` 字段和既有密码均不被迁移改写。

新增迁移 `20260926000100_AddAccountRecoveryCodeHash` 只增加可空字段，代码和迁移仅保存到本地。
**本次开发没有执行真实数据库迁移。** 更新已有服务器前先备份，并检查待执行的全部迁移；
首次启动全新 Docker 数据库的初始化流程与更新现有数据库不是同一件事。
非 EF / 内存演示存储不支持原子恢复，接口会明确返回 `recovery_unavailable`，不会生成无法使用的凭证。

### 客户端接口

统一响应为 `{ success, code, message, recoveryCode? }`，只有成功注册、恢复、签发才带新恢复码。
旧注册请求保持兼容，客户端必须展示并提醒保管新的可选字段，不能在日志中记录。

| 方法和路径 | JSON 请求 |
|---|---|
| `POST /api/registration/create` | `loginName, password, confirmPassword, securityCode?` |
| `POST /api/registration/change-password` | `loginName, oldPassword, newPassword, confirmNewPassword` |
| `POST /api/registration/reset-password` | `loginName, recoveryCode, newPassword, confirmNewPassword` |
| `POST /api/registration/recovery-code` | `loginName, currentPassword` |
| `GET /api/registration/text?culture=zh-CN` | 无账号或密码参数，仅返回公开 UI 字符串 |

恢复码接受分组或连续 64 位十六进制，也接受小写。用户名限制 3-10 位 ASCII 字母/数字，
密码限制 3-20 位无空格 ASCII 可见字符，这是经典客户端协议限制。
提交后的响应消息与页面选择的 `?culture=` 保持一致。

### 离线验证范围

`deploy/tests/test-player-portal.mjs` 可以用现有 Node + Playwright 和 Caddy 二进制验证页面及代理。
测试仅启动自己的随机 localhost 端口和伪上游，不连接游戏、真实数据库或外部证书服务。
Caddy 路由探针临时关闭 TLS；它证明白名单和后台隔离，不证明公网 DNS、ACME 或云防火墙可达。
实际 Docker 构建、证书签发、多设备真实云端连接仍需在目标服务器验收。

The website is built from the [docs-website](../docs-website) folder of this
repository, so the links above work on GitHub as well.
