# Controller pinyin runtime

The client loads librime dynamically. Install the runtime matching the client
architecture before configuring or building MuMain:

```powershell
pwsh ./tools/rime/Install-RimeRuntime.ps1 -Architecture x86
pwsh ./tools/rime/Install-RimeRuntime.ps1 -Architecture x64
```

`runtime.json` pins the official librime release URLs and SHA-256 digests. The
installer refuses an archive whose digest does not match. Runtime binaries are
generated local build inputs and are intentionally ignored by Git; the source
schemas, API header, and upstream licenses remain tracked.
