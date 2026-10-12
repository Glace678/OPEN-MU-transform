# ManifestSigner —— 便携包清单 detached 签名工具

对 `manifest.json` 做 RSA-2048 / PSS-SHA-256 detached 签名，输出 `manifest.json.sig`
（base64 文本）。启动器在 size/SHA256 比对**之前**用内嵌公钥验签（fail-closed）。

## 用法

```powershell
# 签名（发布机）
$env:OPENMU_MANIFEST_SIGNING_KEY = "D:\openmu自用\_安全审计-2026-10-12\keys\manifest-signing-private.xml"
pwsh OpenMU\tools\ManifestSigner\Sign-Manifest.ps1 -ManifestPath <包根>\manifest.json

# 验签（部署机，只需公钥 XML）
pwsh Sign-Manifest.ps1 -ManifestPath <包根>\manifest.json -PrivateKeyXmlPath manifest-signing-public.xml -VerifyOnly
```

## 约定

- 签名对象 = `manifest.json` 原始字节（UTF-8 无 BOM）。
- 密钥 XML 格式 = MS `<RSAKeyValue>`（可同时容纳私钥/公钥字段）。
- 私钥**永不**进入分发物、仓库、备份；打包脚本只在 `OPENMU_MANIFEST_SIGNING_KEY`
  环境变量指向它时才签名，否则仅警告产出"未签名开发包"。
- 开发期跳过验签：设环境变量 `OPENMU_ALLOW_UNSIGNED_MANIFEST=1`（仅警告放行；
  签名存在但错误时永远拒绝）。