# Upstream alignment (fork debt)

This repository contains three upstreams that are vendored into the tree:

| Upstream | What it provides | Local state |
| --- | --- | --- |
| [MUnique/OpenMU](https://github.com/MUnique/OpenMU) | C# server, web admin panel, persistence, tests | ~11,440 files byte-identical; 274 files differ (mostly self-developed additions in new files); 107 files exist only here |
| [sven-n/MuMain](https://github.com/sven-n/MuMain) | Season 5.2 client sources the engine is built on | 13,314 files byte-identical; ~610 files differ with no shared base commit |
| [ptitSeb/gl4es](https://github.com/ptitSeb/gl4es) | Desktop GL → GLES translation layer, vendored | bidirectional fork: local `es_passthrough` patch **and** missing upstream rendering fixes |

## Why there is no base SHA

The original import did not record the commit each tree was taken from, so a plain
`git diff` against upstream HEAD cannot be turned into a per-file merge plan: most of
the differences come from upstream's own later work, not from local edits. Everything
below is therefore recorded as *evidence of missing upstream fixes*, not as a diff.

## Confirmed "upstream fixed it, we did not"

| Area | Upstream state | This tree | Action |
| --- | --- | --- | --- |
| `SellItemToNpcAction.cs` | `IsSellableToNpc: false` guard before price calculation | guard missing (fixed here on 2026-10-01, together with the `ItemDefinition.IsSellableToNpc` data model flag, the EF migration and the `AddIsSellableToNpcFlag` update) | keep in sync when merging |
| `wchar_t` migration (`NewUIGoldBowmanWindow.cpp`, `npcBreeder.cpp`, `w_BuffScriptLoader.cpp`, `ServerMsgWin.cpp`) | `sizeof(wchar_t)` on wide buffers | still `sizeof(char)` in places (fixed here on 2026-10-01; `Winmain.cpp`'s `char symbolBuffer[sizeof(SYMBOL_INFO) + MAX_SYM_NAME * sizeof(char)]` is **correct** — the symbol API is narrow — and must not be "fixed") | verify against upstream on the next merge |
| `NewUIMainFrameWindow.cpp` / `UITransform.h` | HUD scaling rework, `constexpr kHudTop` etc. | not merged | functional gap; HUD layout diverges on non-4:3 resolutions |
| `CharacterManager.cpp` | wing/model detection refactor | not merged | functional gap |

## gl4es

The vendored gl4es tree carries a local `es_passthrough` feature (gl4es skips its ES 1.00
converter for the engine's hand-written GLSL ES 3.x shaders) that does not exist upstream,
and it is missing upstream rendering fixes (raster-position clipping in `raster.c`,
getter state tracking, line-stipple in the attribute stack, `GL_GENERATE_MIPMAP_HINT`).
An upgrade must replay the local patch; there is no `.patch` file in the tree yet.

## Recommended procedure (next time the repos are aligned)

1. Add both upstreams as remotes: `git remote add upstream-mu …`, `git remote add upstream-main …`.
2. For each of the two forks, record the closest base commit (`git merge-base`, or the import commit) in this file.
3. Do a three-way diff per file, merging in this order: crash/security fixes → functional fixes → refactors.
4. Re-run the repo checks: `OpenMU` test suites, `OpenMU-HarmonyOS/build-tools` node tests, and the Android/Gradle unit tests.
5. Re-check the gl4es `es_passthrough` behavior on a real Android/HarmonyOS device after any upgrade.
