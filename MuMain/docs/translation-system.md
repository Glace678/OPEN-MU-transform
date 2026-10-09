# Translation system

This document describes the `.resx` -> generated C++ translation system used by
the client. It replaces the older `text.bmd` / `GlobalText` and
`Translations/*.json` / `EDITOR_TEXT` paths, which were removed during the
migration tracked in sven-n/MuMain#347.

If you only want to add or change a string, jump to
[Adding a new string](#adding-a-new-string).

---

## Source files

All translation data lives in `src/Localization/` as standard ResX XML:

```
src/Localization/
  Editor.en.resx     Editor.de.resx     ...
  Game.en.resx       Game.de.resx       ...
  Dialog.en.resx     Dialog.de.resx     ...
```

Filename convention: `<Group>.<locale>.resx`.

- **Group** is a freeform PascalCase name. It becomes the C++ namespace
  `I18N::<Group>` and ends up in `Generated/I18N/<Group>.{h,cpp}`.
- **Locale** is a BCP-47-ish code (`en`, `de`, `pt`, `zh-TW`, ...). The default
  locale is `en` and every group must ship that file; non-default locales are
  optional and fall back to `en` at runtime for any key they don't supply.

A `.resx` file contains `<data name="...">` entries. The `name` attribute is
the **key** (free text), the inner `<value>` is the translated string, and an
optional `<comment>legacy_id=N[,N,...]</comment>` carries integer IDs that the
old `GlobalText[N]` call sites still need for lookups by index.

Example (`src/Localization/Game.en.resx`):

```xml
<data name="Beep sound for whispering" xml:space="preserve">
  <value>Beep sound for whispering</value>
  <comment>legacy_id=387</comment>
</data>
<data name="Sound volume" xml:space="preserve">
  <value>Sound volume</value>
</data>
```

The matching German file uses the same `name=` keys with translated `<value>`s:

```xml
<data name="Beep sound for whispering" xml:space="preserve">
  <value>Piepton zum Flüstern</value>
  <comment>legacy_id=387</comment>
</data>
<data name="Sound volume" xml:space="preserve">
  <value>Soundlautstärke</value>
</data>
```

## Build-time generator

`tools/ResxGen` is a small .NET console app driven from `src/CMakeLists.txt`.
On every build it scans `src/Localization/*.resx`, groups files by stem, and
emits:

```
${CMAKE_BINARY_DIR}/Generated/I18N/
  All.h    All.cpp       <- master entry points (SetLocale, Format, ...)
  Editor.h Editor.cpp
  Game.h   Game.cpp
  Dialog.h Dialog.cpp
  Metadata.h
```

The custom command is wired up in `src/CMakeLists.txt` under the comment
"ResxGen: .resx -> typed C++ accessors". `add_dependencies(MuClient ResxGen)`
guarantees the generator runs before the client compiles, and the generated
`.cpp` files are added to the `MuClient` target via `target_sources`. Adding,
renaming, or removing a `.resx` re-runs the generator automatically because
CMake reglobs with `CONFIGURE_DEPENDS`.

The generator takes two flags:

- `--input <dir>` - the `.resx` source directory (always `src/Localization`).
- `--output <dir>` - where to write `Generated/I18N/`.
- `--wide-groups <Group,...>` - groups whose strings are `wchar_t*` instead of
  `char*`. Today: `Game,Dialog`. Add a new group here if it needs wide chars
  (most do; the `Editor` group is the only narrow one).

## Generated C++ API

### Per-group accessors

For every group, ResxGen emits one `extern` slot pointer per resource entry.
The slot is updated in place when the locale switches, so call sites never
have to be re-read:

```cpp
#include "I18N/All.h"

g_pRenderText->RenderText(x, y, I18N::Game::SoundVolume);
ImGui::Text("%s", I18N::Editor::SaveSkills);
```

The identifier is derived from the resx `name=` key by stripping non-ASCII
and PascalCasing it. For example:

| Resx key                         | C++ identifier                  |
|----------------------------------|---------------------------------|
| `Sound volume`                   | `I18N::Game::SoundVolume`       |
| `Beep sound for whispering`      | `I18N::Game::BeepSoundForWhispering` |
| `Close388`                       | `I18N::Game::Close388`          |
| `[error5] User has selected ...` | `I18N::Game::Error5UserHasSelectedTheExitButton` |

If you need to know the exact identifier before building, look at
`Generated/I18N/<Group>.h` after one build, or follow the rule above. Two keys
that slug to the same identifier are a build error from the loader.

### Lookup by legacy integer ID

Code paths that historically used `GlobalText[N]` keep working through a
binary-search lookup:

```cpp
mu_swprintf_s(buf, L"(%ls)", I18N::Game::Lookup(guildTextIndex));
```

`Lookup(int)` is generated only for groups that have at least one entry with
a `legacy_id=` comment, and it's `static_assert`-checked to be strictly sorted
so duplicate IDs fail the build (`Generated/I18N/<Group>.cpp`).

### Format / placeholder substitution

For strings with `{0}`, `{1}`, ... placeholders, use `I18N::Format`:

```cpp
const auto msg = I18N::Format(I18N::Editor::ErrorIndexAlreadyInUse, { idxStr });
```

`{{` and `}}` are escaped to literal `{` and `}`. The first argument is a
`const char*` format string (UTF-8); the second is an
`std::initializer_list<std::string_view>` of values to splice in. Result is a
`std::string` (UTF-8). Wide-group strings use `%s` / `%ls` directly through
the bounds-checked `mu_swprintf_s` (or `swprintf_s`) rather than `Format`.
Avoid `wsprintf` and `mu_swprintf` - they don't take a destination size and
will overflow the buffer if a translated string is longer than expected.

### Master entry points

`I18N/All.h` exposes the global API:

```cpp
namespace I18N {
    // Switches every group to the given locale, then fires observers.
    // Unknown locale falls back to the default ("en").
    void SetLocale(const char* locale) noexcept;

    // Returns the locale code passed to the last successful SetLocale
    // (or the default before any explicit call). Never null.
    const char* GetCurrentLocale() noexcept;

    // Returns the BCP-47 codes this build was generated with, default
    // locale first. Span backs static storage.
    std::span<const char* const> GetAvailableLocales() noexcept;

    // Returns the position of `locale` in GetAvailableLocales (0-based,
    // default at 0). Returns -1 for unknown.
    int LocaleIndex(const char* locale) noexcept;

    // Returns the display name of `locale` in that locale's own language
    // (e.g. "Deutsch" for "de"). Returns `locale` itself for codes the
    // generator has no display name for.
    const char* GetLanguageDisplayName(const char* locale) noexcept;

    // {0},{1}-style substitution for narrow strings.
    std::string Format(const char* format,
                       std::initializer_list<std::string_view> args);

    // Observer callback used by UI widgets that cache I18N strings.
    using LocaleObserver = void (*)(void* context) noexcept;
    void RegisterLocaleObserver(LocaleObserver cb, void* ctx) noexcept;
    void UnregisterLocaleObserver(LocaleObserver cb, void* ctx) noexcept;
}
```

## Locale switching at runtime

With Simplified Chinese selected, stock server announcements for Blood Castle,
Chaos Castle, Devil Square, and Happy Hour are displayed in Chinese, including
entrance countdowns and closures. Custom announcements and guild messages keep
their original wording. Notice wrapping and line spacing follow the current
font size and window scale. The map/coordinate label stays vertically centered
inside its visible frame when the window or UI font changes.

Character selection info balloons expand to fit their three text rows. Button
and radio-button captions stay centered and shrink only when needed to fit the
button. Scrollable text panels rewrap when the font or window scale changes;
chat and item tooltip rows use the measured font height rather than fixed pixel
spacing.

The active locale is selected through the Option window's language dropdown
(see `src/source/UI/NewUI/Options/NewUIOptionWindow.cpp`). It calls
`I18N::SetLocale(code)` and persists the choice via `GameConfig.UILocale`
(the single source of truth for the active locale; on startup
`src/source/Data/GameConfig/GameConfig` re-applies it).

`SetLocale` does three things:

1. Resolves `locale` to a `LocaleIndex` (unknown -> default 0).
2. Calls each group's generated `ApplyLocale(int)` (a data-driven `kSlots`
   table that overwrites every `extern` pointer in the group).
3. Fires every registered `LocaleObserver`.

Most rendering picks up the change for free, because text is read straight
from the now-updated `I18N::<Group>::Name` slot on the next frame. Widgets
that **cache** strings (button tooltips, list labels assembled once at
construction) need to re-read them on locale change. The pattern is:

```cpp
class CCharInfoBalloon {
public:
    CCharInfoBalloon() {
        I18N::RegisterLocaleObserver(&OnLocaleChanged, this);
    }
    ~CCharInfoBalloon() {
        I18N::UnregisterLocaleObserver(&OnLocaleChanged, this);
    }
private:
    static void OnLocaleChanged(void* ctx) noexcept {
        auto* self = static_cast<CCharInfoBalloon*>(ctx);
        if (self->m_pCharInfo != nullptr) {
            self->SetInfo();  // re-reads I18N::Game::* into its caches
        }
    }
};
```

`NewUIButton`, `NewUIRadioButton`, and `NewUICheckBox` also have
`ChangeText(const wchar_t* const*)` / `ChangeToolTipText(...)` overloads that
take a slot pointer; widgets created with those overloads refresh
automatically without needing their own observer.

## Fallback behaviour

- Active locale missing a key -> falls back to the default locale (`en`) for
  that key. The fallback is built into the generated `kSlots` table, so it
  costs nothing at runtime.
- Active locale entirely absent -> `SetLocale` resolves it to the default
  locale silently.
- Unknown locale code -> default locale.
- Lookup by legacy ID that doesn't exist in the group -> returns an empty
  string. The generator's static_assert prevents duplicate IDs from compiling.

There's no per-key "key name shown in red" debug mode today; missing keys
just fall back. If you want a hard-fail mode for new development, the
generator is the place to add it (`tools/ResxGen/CppEmitter.cs`).

## Adding a new string

1. Add a `<data name="...">` entry to `src/Localization/<Group>.en.resx`.
   Pick a key that reads as English text - it doubles as the source-of-truth
   value and as the slug for the C++ identifier.
2. If you have translations ready, add entries with the same `name=` to the
   other `<Group>.<locale>.resx` files. Missing translations fall back to
   English - it's fine to ship the English entry alone and translate later.
3. Use it from C++ as `I18N::<Group>::<PascalCaseSlug>`. No rebuild dance
   needed; CMake re-runs the generator automatically on the next build.

If your new string contains placeholders, prefer `{0}`/`{1}` and `I18N::Format`
for narrow groups. For wide groups, `%s` / `%ls` via the bounds-checked
`mu_swprintf_s` is the existing pattern.

## Adding a new locale

1. Create `src/Localization/<Group>.<newLocale>.resx` for every group you want
   the new locale to cover. Missing groups fall back to `en` at runtime.
2. Add a display name for the locale to
   `tools/ResxGen/CppEmitter.cs#KnownLanguageDisplayNames` so the language
   dropdown shows it in its own language (e.g. `["fr"] = "Français"`).
3. Build. `ResxGen` picks up the new locale automatically from the filename;
   `GetAvailableLocales` will include it next run.

The client currently exposes fifteen UI locales: `en`, `de`, `es`, `fr`, `id`,
`ja`, `ko`, `pl`, `pt`, `ru`, `tl`, `uk`, `vi`, `zh-CN`, and `zh-TW`. The
`fr`, `ko`, and `vi` files are deliberate locale markers while their
translations are being completed: the `Dialog`, `Editor`, and `Metadata` groups are empty shells, but `Game.fr`/`Game.ko`/`Game.vi` ship only the four account self-service strings (`AccountRegister`, `AccountChangePassword`, `AccountResetPassword`, `AccountPortalUnavailable`); every other group and
key resolves to the English value through the normal ResxGen fallback. This
keeps the selector and generated locale registry honest without presenting
English placeholder text as a completed translation.

UI locale and BMD data locale are independent. Existing BMD packages live under
`src/bin/Data/Local/<dir>` and use legacy directory names such as `Eng`, `Ger`,
and `Chs`; the options selector maps `fr`, `ko`, and `vi` to the existing
`Eng` package because no `Frn`, `Kor`, or `Vie` package is shipped yet. All
data-backed paths therefore continue to resolve to a real package instead of
attempting to open a missing directory. When a complete BMD package is added,
change only that selector mapping and add the corresponding directory; the
ResxGen locale code does not need to change.

## Auditing All Resource Locales

The presence of a locale in the selector does not mean that its translations
are complete. Produce a read-only coverage and format report before releasing
localization changes:

```powershell
dotnet run --project tools/Localization/LocalizationTool.csproj -c Release -- report `
  --input src/Localization --output out/localization/coverage.json

# Limit the report to one existing locale when reviewing a translation.
dotnet run --project tools/Localization/LocalizationTool.csproj -c Release --no-build -- report `
  --input src/Localization --output out/localization/de.json --locale de

# Dependency-free regression checks, including the real tutorial resources.
dotnet run --project tools/Localization.Tests/LocalizationTool.Tests.csproj -c Release -- src/Localization
```

The report includes every English resource group for every discovered locale.
It counts non-empty source keys present, missing keys that fall back to English,
empty values, text identical to English, and placeholder signature mismatches.
Empty values are separate: ResxGen preserves them instead of falling back.
These counts measure structural coverage, not linguistic quality or native
font and shaping support.

Missing files or keys, empty values, placeholder mismatches, and tutorial lines
over the current 99-character buffer limit are errors. The JSON report is still
written when these exist, and the command returns exit code 1. Suspected legacy
encoding damage, differing line breaks, extra keys and duplicate entries are
review warnings only. Accented European text is not judged by the Simplified
Chinese-specific mojibake heuristic. The existing `audit --locale zh-CN`
command retains its stricter regional terminology checks.

The `fr`, `ko`, and `vi` locale markers currently have no translations; their
English fallback is reported as missing coverage, not successful localization.
The new player guide has eight translated keys in all other shipped locales,
including Japanese and Traditional Chinese. Japanese Editor and Metadata
resources remain incomplete and are still reported separately.

The regression checks also cover the 35 repaired legacy printf translations in
German, Spanish, Indonesian, Japanese, Portuguese, and Russian. These bounded
checks preserve argument type and order and require literal percentages to use
`%%`; translated text is not replaced with English to pass the audit.
The `Game.OnePercentLow` performance label is rendered directly, not as a printf
format. The report explicitly classifies only that exact group/key as plain
text because the English `1% Low` label resembles a `%Lo` conversion. Its
correct translations therefore do not produce false placeholder errors.
The console prints the classification and reason; JSON includes
`plainTextClassifications`, while each group row's `plainTextKeys` counts its
non-empty classified values. These keys still count toward presence and
English-equality statistics, and missing, empty or suspect text is checked as
usual. No other group/key is exempt from printf checks. Keep this label as
normal display text rather than adding arguments or doubled percent signs.
The strict regional `audit` command shares this same exact classification;
its terminology, corruption and line-break checks remain unchanged.

## Regenerating Mainland Chinese

`zh-CN` is a separate BCP-47 culture from `zh-TW`; never reduce either locale
to the two-letter `zh` code. The client ships complete `zh-CN` resources for
`Game`, `Dialog`, `Editor`, and `Metadata`.

The first draft for `Game`, `Editor`, and `Metadata` is generated from the
Traditional Chinese resources with OpenCC's `tw2sp` profile. `Dialog` has no
Traditional Chinese source and is translated from English. In both cases,
reviewed Mainland terminology in `tools/Localization/zh-cn-overrides.json`
takes precedence over automatic conversion.

Install the pinned converter and build the localization tool:

```powershell
cd tools/Localization
npm ci --ignore-scripts
cd ../..
dotnet build tools/Localization/LocalizationTool.csproj -c Release
```

Regenerate and audit the resources:

```powershell
dotnet run --project tools/Localization/LocalizationTool.csproj -c Release --no-build -- generate-zh-cn `
  --input src/Localization `
  --overrides tools/Localization/zh-cn-overrides.json `
  --replacements tools/Localization/mainland-banned-terms.tsv `
  --context-terms tools/Localization/mainland-context-terms.json `
  --opencc-module tools/Localization/node_modules/opencc

dotnet run --project tools/Localization/LocalizationTool.csproj -c Release --no-build -- audit `
  --input src/Localization `
  --locale zh-CN `
  --banned-terms tools/Localization/mainland-banned-terms.tsv `
  --context-terms tools/Localization/mainland-context-terms.json

tools/Localization/Test-ZhCnFontCoverage.ps1
```

The audit requires the same resource keys as English, non-empty values,
matching printf and indexed placeholders, valid text without mojibake, and no
reviewed Taiwan-only or contextually incorrect terms. Add broad regional word
choices to `mainland-banned-terms.tsv`; add key-scoped game terminology such as
`party -> 队伍`, `guild -> 战盟`, and `vault -> 仓库` to
`mainland-context-terms.json`. Use explicit overrides for commands, proper
nouns, and strings where the same English word has different meanings.
The font check verifies that the bundled Regular and Bold faces declare the
expected `Noto Sans CJK SC` family and contain every Unicode code point used by
the four Simplified Chinese resource groups.

New player configurations default to `zh-CN`. If the font setting remains at
`Default`, that locale resolves to the bundled `Noto Sans CJK SC` face.
Portable clients use bundled `Noto Sans CJK JP` for Japanese, `Noto Sans CJK TC`
for Traditional Chinese, and `DejaVu Sans` for other UI languages. Windows
keeps its historical `Yu Gothic`, `Microsoft JhengHei`, and `Tahoma` defaults.
An explicit configured font always takes precedence over these defaults.
The existing legacy data-language priority is preserved so Japanese or Chinese
BMD text can also select a CJK face. English remains the
resource fallback for a missing key and the legacy `Language=Eng` protocol
setting remains separate from the UI locale.

The bundled JP, TC, and SC faces are the unmodified static Regular/Bold OTFs
from the official `Sans2.004` release, not a moving branch or regional subset.
`src/bin/fonts/NotoSansCJK-NOTICE` records the pinned commit, copyrights and
SHA-256 values; `NotoSansCJK-LICENSE` contains the complete OFL. Desktop asset
copying and mobile data packaging include the entire fonts directory, including
these notices. Codepoint coverage checks do not establish correct regional
glyph shapes or replace native visual acceptance.

Some legacy Game keys contain damaged non-ASCII source text but still have a
valid `legacy_id`. `ResxGen` emits these as stable `Legacy<id>` identifiers so
their reviewed translations remain reachable through `Game::Lookup(int)`.

## Migration history

If you're trying to understand why a string lives in resx instead of the old
data files, two commits are the headlines:

- `4a1fd53f` - removed `Translator` / `Translations/*.json` (Editor group).
- `a58ac50d` - removed `GlobalText` / `Text_*.bmd` (Game group).
- `19ad1888` - removed the per-language `Dialog_*.bmd` files after the Dialog
  group migration.

The leftover binary `text.bmd` / `NPCDialogue.bmd` / per-language
`Dialog_*.bmd` files are pure data; nothing in the C++ source loads them
anymore.
