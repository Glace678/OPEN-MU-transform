# OpenMU — Third-Party Components / NOTICES

本文件汇总 OpenMU 发行所依赖的第三方组件及其许可证要点（DEP-016）。
完整传递闭包清单不在此静态枚举；构建期可由以下命令生成：

- .NET: `dotnet list OpenMU/src/MUnique.OpenMU.sln package --include-transitive`
- docs-website: `npm ls --all`（位于 OpenMU/docs-website）

## 许可证锁定项（不得随意升级，原因见 Directory.Packages.props 内联注释）

| 组件 | 锁定版本 | 许可证 | 维持现状原因 |
|---|---|---|---|
| MathParser.org-mXparser | 4.4.2 | 旧版宽松双许可 | DEP-018: 新版本许可证变为非 OSS/商业条款,故锁定 4.4.2 |
| SixLabors.ImageSharp | 2.1.11 | Six Labors Split License(免费用于 OSS) | DEP-019: 3.x 转商业/非免费许可;2.x 为最后宽松线 |
| SixLabors.ImageSharp.Drawing | 1.0.0 | 同上 | DEP-019: 2.x 仍为 prerelease,不在生产引入 |
| NUnit / NUnit3TestAdapter | 3.14.0 / 4.6.0 | MIT | 升级会破坏插件测试,按既有注释锁定 |

## 其余直接 .NET 依赖(择要)

- Dapr.* 1.16.1 - Apache-2.0
- Microsoft.EntityFrameworkCore.* 10.0.2 - MIT
- OpenTelemetry Exporter.* 1.15.3 - Apache-2.0
- Serilog.* - Apache-2.0
- BCrypt.Net-Next 4.0.3 - MIT
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0 - PostgreSQL License

## docs-website(npm)

- Docusaurus 3.10.2(MIT 平台)及传递依赖;敏感传递包已在 package.json `overrides` 对齐到修复版(brace-expansion/fast-uri/image-size/joi/js-yaml/qs/svgo)。

## 缺口说明

- 未逐一枚举全部 NuGet/npm 传递依赖的具体许可证文本;完整 NOTICE 生成需接入构建期工具(如 dotnet-project-licenses / license-checker)。本文件作为治理基线,覆盖全部"许可证敏感/锁定"决策项。
