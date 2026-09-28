# Options Window and Config

Behaviour changes that landed in PR
[#335](https://github.com/sven-n/MuMain/pull/335) for the options window
and the `config.ini` it writes to.

For the camera architecture see [`camera-system.md`](camera-system.md).

The item explanation window supports arbitrary resized window widths, including
1366, 1920 and 2560 pixels. It uses the nearest smaller legacy layout, with a
minimum layout below 800 pixels, so opening item help at a nonstandard resolution
does not divide by zero.

Keyboard presses are retained until the next game frame. A quick tap of an
inventory, map, or other shortcut is recognized even when SDL receives the key
press and release between two rendered frames. Losing focus clears pending
presses so they do not fire after returning to the game.

Shop confirmation text wraps within the available lines without displaying
uninitialized trailing characters. Release builds hide internal shop packet
identifiers and script-version diagnostics.

---

## What changed

The options window is now **the same window in every scene** (login,
character select, gameplay) - it shares one layout, one apply path, and one
backing config object. There is no separate per-scene options dialog.

What was added on top of that:

- **Resolution and windowed/fullscreen toggle apply without restarting.**
  Switch them at any time from the options window.
- **Volume and render-level sliders round to the nearest level** instead of
  truncating, so the slider endpoints actually reach 0 (mute) and the
  configured maximum.
- **Resolution combo-box clicks no longer leak through** to the checkboxes
  behind the dropdown.
- **Resolution changes go through SDL** (PR
  [#530](https://github.com/sven-n/MuMain/pull/530)), which makes the live
  apply actually work in windowed mode on Windows. Windowed resizes re-center
  the window on the display. Fullscreen picks the closest real display mode
  (or borderless desktop when nothing matches), and the combo and
  `config.ini` record the mode that actually resulted, not the one requested.

## What it stores in `config.ini`

Standalone clients write next to the executable as `config.ini`. The local
launcher instead keeps each player's settings in `Data/Keys/game-config.ini`,
so changing options does not invalidate the installation's file checks.
New configurations start fullscreen; existing saved window preferences are
preserved. The options window writes to these sections:

- `[Window]` - width, height, windowed flag.
- `[Graphics]` - render levels, vsync, fps limit.
- `[Audio]` - volumes.
- `[Login]` - language, and the remembered-credential keys (`RememberMe`,
  `SavePassword`, `EncryptedUsername`, `EncryptedPassword`); see
  "Remembering login credentials" below.
- `[ConnectionSettings]`
- `[Camera]` - orbital wheel-zoom radius (`Zoom`). **New in this PR**, the
  only key the camera rework added.

Missing keys fall back to compile-time defaults from
`src/source/Data/GameConfig/GameConfigConstants.h`. If `config.ini` doesn't exist,
it's created the first time anything calls `Save()`.

## Remembering login credentials

The login screen has two separate checkboxes:

- **Remember Username** - fills in your account name next time. Safe to leave
  on; only the username is stored.
- **Remember Password** - also stores the password. Because a saved password
  lets anyone using the same computer log into your account, ticking it opens a
  confirmation dialog first; the box only stays ticked if you accept it. Leave
  this off on shared machines (e.g. an internet cafe).

Editing the account or password field clears any stored credentials and turns
Remember Password back off, so an out-of-date password is never left behind.

Stored credentials are obfuscated (not plaintext) and tied to the current
machine and user account, so copying `config.ini` to another machine does not
reveal them. This is protection against a casual reader of the file, not a
determined attacker - which is why saving the password is opt-in.
