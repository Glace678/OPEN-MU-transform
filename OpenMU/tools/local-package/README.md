# OpenMU Local package

The Windows package manifest must include `App/Game/MUnique.Client.Library.dll`
as well as `Main.exe`: the protocol library is required to connect to the game
server. An incomplete package is rejected before starting the database.

The solo profile grants three additional stat points per normal level, on top
of the character's class and quest rewards. These points are awarded on level
up; logging in never rewrites the character's saved stats. Older solo databases
which stored this bonus under `PointsPerLevelUp` remain usable without a reset.

## Separate desktop entries

The shared lifecycle implementation is now built by
`src/LocalLauncher.Core`. The existing Windows Forms launcher references that
library without changing its portable data layout.
`src/GameLauncher` and `src/GmLauncher` provide independent `OpenMU-Game` and
`OpenMU-GM` entry points for desktop platforms. GM opens the existing web
administration system; it is not yet a standalone native GM interface.

`Build-DesktopPackage.ps1` stages an **unverified development package** on a
matching Linux/macOS host. It requires PowerShell 7, .NET 10, an already-built
native MuMain payload and a relocatable native PostgreSQL runtime with its
license. ELF/Mach-O architecture checks reject Windows binaries and incorrect
architectures. The script requires a fresh destination, seeds clean game
settings, publishes self-contained server/launchers and verifies file hashes.
The game payload must include a native `librime.so` or `librime.dylib` beside
`Main`, with its runtime dependencies, for Chinese controller input. A Windows
`rime.dll` copied by an earlier asset build cannot satisfy this requirement.

```powershell
./tools/local-package/Build-DesktopPackage.ps1 `
  -Runtime linux-x64 `
  -GameDirectory /path/to/MuMain/build-linux/src `
  -PostgreSqlDirectory /path/to/postgresql-runtime `
  -PostgreSqlLicense /path/to/postgresql-runtime/COPYRIGHT `
  -OutputDirectory /path/to/new/OpenMU-Solo
```

The Unix entries use private per-user storage, native executable names and
owner-only credential files. Windows continues to use DPAPI. Desktop GUI
first-run setup, native dependency acceptance and macOS signing/notarization
are not finished. Read `DESKTOP-README.txt` for the explicit development-stage
limits. No Android APK or iOS IPA can be produced by this script.

Android/iOS still require a mobile renderer and touch UI, an embedded
persistent server/database design compatible with their lifecycle, a mobile
GM interface, native SDK builds and device validation. Existing desktop
PostgreSQL subprocesses and dynamic server plug-ins must not be advertised as
an offline mobile implementation.

### Validation on 2026-09-05

- Linux x64: compiled the native MuMain executable and NativeAOT network
  library in Ubuntu 24.04 under WSL. Both are ELF x86-64; `ldd` reported no
  unresolved dependencies in that build environment.
- The 39 core input/timing tests passed on both Windows and Linux (20,296
  assertions per platform). These
  include virtual-controller tests, not physical motor acceptance.
- The existing Rime runtime smoke test now runs on Windows and Linux. Both
  selected and committed the Chinese candidate for `nihao`. CMake stages the
  native Unix Rime library; its dependent libraries still need clean-host
  distribution verification.
- Separate Linux game/GM bootstrap executables ran their read-only `--probe`
  command. Their complete PostgreSQL/game distribution has not been staged.
- Focused launcher tests passed: 28 on Windows and 26 on Linux, including
  native Unix private-file permission checks.
- Cash shop tests passed: 28 service/packet tests and 8 initialization/model
  checks. The initialized solo catalog contains 169 permanent offers. These
  do not establish real PostgreSQL transaction or in-game UI acceptance.
- Windows private test package `0.9.10-solo.2` was rebuilt and archived.
  All 23,512 immutable files passed SHA-256 verification; the archived client
  matched the latest build, login settings were clean and no user data files
  were present. Computer Use reached the first-run administrator setup dialog;
  authentication was left to the user, and another test server's ports were
  not disturbed.
- All 117 controller workflows remain `PendingRuntime` in the companion
  client's coverage registry. Per-monster/mount visual frame-rate checks,
  physical motor feedback and a full solo progression run remain unverified.
- Unix clean-host dependency packaging, desktop first-run GUI, macOS native
  builds and signing, and Android/iOS implementation remain open. No four-platform
  ready-to-play release is available.

## Windows package

`Build-LocalPackage.ps1` produces the private-use Windows package described by
the project plan. It publishes the server as a self-contained, untrimmed,
multi-file application and publishes `OpenMU-Local.exe` as a self-contained
single-file launcher.

Release staging requires PowerShell 7, the .NET 10 SDK, a complete Windows
x64 MuMain publish directory, a licensed Visual Studio installation which
provides the matching x64 app-local CRT redistributables, and the pinned
PostgreSQL archive SHA-256 in `postgresql-runtime.json`. None of these
build-time dependencies are required on the machine which runs the completed
self-contained package.

The launcher starts the included 2.04d MuMain client explicitly against
`127.0.0.1:44406`; it does not rely on any client-side address or port default.
With the default output option, the unpacked launcher is written to
`artifacts/OpenMU-Local/OpenMU-Local.exe` and the distributable archive to
`artifacts/OpenMU-Local-<version>-win-x64.zip`.

The PostgreSQL runtime is intentionally not committed. The pinned version,
official download URL, and verified SHA-256 live in `postgresql-runtime.json`.
The release operator may independently verify and override the expected value
with `-PostgreSqlSha256`; the download script rejects a missing, malformed, or
mismatched hash. The completed package manifest records the version, source URL,
and resolved archive hash together with per-file hashes. The compatibility
`App/Game/config.ini` seed is excluded because it may be changed by a direct
client launch; the launcher initializes player settings from the verified
`App/Game/config.ini.template` instead.

Example after the hash has been independently verified:

```powershell
.\tools\local-package\Build-LocalPackage.ps1 `
  -GamePublishDirectory 'D:\path\to\MuMain\Release'
```

Pass `-PostgreSqlArchivePath` to reuse an already downloaded archive. The local
file is copied into an isolated temporary directory and must match the same
operator-supplied SHA-256 before extraction:

```powershell
.\tools\local-package\Build-LocalPackage.ps1 `
  -GamePublishDirectory 'D:\path\to\MuMain\Release' `
  -PostgreSqlArchivePath 'D:\downloads\postgresql-windows-x64-binaries.zip'
```

To stage only the pinned PostgreSQL runtime, use:

```powershell
.\tools\local-package\Download-PostgreSql.ps1 `
  -SourceArchive 'D:\downloads\postgresql-windows-x64-binaries.zip' `
  -Destination '.\artifacts\OpenMU-Local\Runtime\PostgreSQL'
```

The destination must end in `Runtime\PostgreSQL`. Existing reparse points are
rejected, and an existing verified runtime is moved aside until the replacement
has been completely validated and installed.

After verification, staging removes pgAdmin, StackBuilder, development headers,
debug symbols, and server documentation from the temporary payload. The local
stack retains PostgreSQL's `bin`, `lib`, and `share` runtime trees plus the
server and command-line third-party license texts; the package copies both
license texts into `Licenses/`.

The package also deploys the complete x64 Microsoft Visual C++ CRT directory
beside both `Main.exe` and the PostgreSQL executables. Pass
`-VisualCppRuntimeDirectory` when the script cannot discover the active Visual
Studio `x64/Microsoft.VC*.CRT` directory. Pass
`-VisualCppRedistNoticePath` with Visual Studio's
`Licenses/1033/Redist.txt` when it cannot be inferred from the runtime path.
Packaging fails closed if either input is unavailable, so the archive never
silently depends on a machine-wide VC++ Redistributable.

The build sets `ci=true`, disables build servers, disables project parallelism,
and uses one MSBuild node. This prevents repository pre-build generators and npm
documentation tasks from recursively starting nested tool processes during the
release publish.

Before a full release build, run the focused packaging safety test:

```powershell
.\tools\local-package\Test-GamePayloadPackaging.ps1
```

It verifies that the mutable developer `config.ini` cannot leak remembered
credentials into the package, the reviewed template selects `zh-CN`, linker
libraries are excluded, and recursive cleanup rejects directory reparse points.

The game assets in the companion MuMain tree do not have confirmed public
redistribution permission. Packages created by this script are for local use
only until the asset licenses have been clarified.

## Package layout

### Unsigned cloud builds

`tools/cloud-build/Export-BuildSnapshot.ps1 -OutputDirectory <new-directory>`
exports only changed source files and the required source assets. It rejects
saved databases, keys, logs, executable outputs and recognizable access tokens.
Upload the resulting snapshot to a private build repository and run the
`OpenMU Unsigned Desktop` workflow manually. Do not put GitHub tokens in files.

The workflow builds Linux x64 and macOS Apple Silicon on native hosted runners.
The game and GM launchers share a graphical first-run setup, backup and stop
interface. Both launchers must remain beside the package's App and Runtime
directories. These are not yet independently relocatable macOS app bundles.

Archives contain the native client, network library, source-built PostgreSQL,
self-contained .NET applications and redistributable native dependencies.
Linux requires an Ubuntu 24.04-compatible desktop with the host's graphics
driver. macOS artifacts have no Developer ID signature or notarization;
ad-hoc signatures only make rewritten native libraries locally executable.
The scripts do not disable Gatekeeper.

The cloud run checks input/timing tests, native Rime input, package hashes and
a disposable database start/backup/restore/stop cycle, including repeated
connect-server handshakes. Consult `verification.json` for the actual scope.
Neither a green build nor these checks certify physical haptics, a complete
playthrough, Android/iOS support, or public asset redistribution rights.

The resulting archive contains:

```text
OpenMU-Local/
  OpenMU-Local.exe
  App/Game/
  App/Server/
  Runtime/PostgreSQL/
  Data/PostgreSQL/
  Data/Keys/
  Data/Logs/
  Data/Backups/
  Licenses/
  manifest.json
```

`manifest.json` covers every immutable packaged file. Persistent database,
keys, logs, and backups are deliberately excluded because they change during
normal use.

## First run

1. `OpenMU-Local.exe` restricts `Data/` to the current Windows user and Local
   System, then asks for a management password of at least 12 characters.
2. Database and role passwords are generated randomly and stored with
   current-user DPAPI.
3. The launcher validates the package manifest, free space, and all fixed
   ports before initializing PostgreSQL with UTF-8 and SCRAM authentication.
4. PostgreSQL, the OpenMU server, and the 2.04d client start in that order. All
   listeners are fixed to `127.0.0.1`; initial data contains one game server
   and no test accounts.
5. Closing the window leaves the launcher in the notification area. Optional
   Windows startup uses the current user's HKCU Run key and starts only the
   backend in `--background` mode.

On Windows, PostgreSQL tools can fail when UTF-8 initialization receives a
non-ASCII executable, data, password-file, log, or dump path. The launcher
therefore creates ASCII-only directory junctions below
`%ProgramData%\OpenMU-Local\Aliases`. If local policy denies creation there,
it falls back to `%Public%\Documents\OpenMU-Local\Aliases`. The alias is isolated by hashes of the
current user's SID and package path, and its physical parent is restricted to
the current user and Local System. Runtime, database, key, log, and backup
junction targets are validated before every PostgreSQL command; an existing
ordinary directory, reparse-point parent, or mismatched target stops startup.
The aliases contain no database copy: persistent files remain under the
package's `Data/` directory and are included by the normal backup workflow.

The ASCII-path unit tests require permission to create temporary junctions in
ProgramData. A disposable package containing `Runtime/PostgreSQL` can also run
the explicit real-runtime test from a Chinese path:

```powershell
$env:OPENMU_POSTGRES_SMOKE_ROOT = 'D:\中文路径\OpenMU-runtime-smoke'
dotnet test .\tests\MUnique.OpenMU.LocalLauncher.Tests\MUnique.OpenMU.LocalLauncher.Tests.csproj `
  --filter InitializeStartReadyAndStopFromUnicodePackage -- `
  NUnit.ExplicitMode=Relaxed
```

The smoke test selects a temporary loopback port and performs `initdb`, start,
readiness/status checks, and fast stop. It then removes its temporary package
`Data` directory and ProgramData smoke aliases; do not point it at a package
which already contains persistent data.

The local launcher skips server selection and the login form and opens normal
character selection, including on first launch. A saved game account and
password take priority, preserving existing characters. Without a saved account,
the local service creates an installation-specific ordinary game account.
The game password is derived from the original private package keys and is not
the administrator password. It is supplied only to the local game process,
never on the command line or in the public game configuration.
Server authentication remains enabled; existing accounts are never reset or
granted GM rights. The management panel can still create additional accounts.

Automatic login makes one attempt per client launch and accepts only the literal
`127.0.0.1` connect and game-server addresses. Missing credentials, unavailable
servers, or login failures retain the normal manual recovery flow; they do not
retry passwords indefinitely. A working server handshake is still required.
Set `AutomaticGameLogin` to `false` in `Data/Keys/local-settings.json` and restart
the launcher and local service to restore manual login. The launcher passes `MU_LOCAL_AUTO_LOGIN=1`
only to the game process; clients started normally keep their existing behavior.

Native visual acceptance builds can opt into MuMain's
`-DENABLE_FRAMEBUFFER_CAPTURE_TESTS=ON`. With `MU_CAPTURE_FRAME` and
`MU_CAPTURE_PATH` set, the client writes one PPM framebuffer; `MU_CAPTURE_SCENE`
optionally limits the counter to one scene. The capture log reports scene,
protocol state and login visibility. This option is off for normal packages.

Normal stop first asks OpenMU to exit through its current-user-only named pipe,
waits for the server process to finish, creates an automatic backup, and then
stops PostgreSQL. A forced stop is only available after explicit confirmation.
