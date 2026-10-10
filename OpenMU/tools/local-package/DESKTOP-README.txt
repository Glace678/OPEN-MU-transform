OpenMU Solo - Desktop Development Package

This is an unverified development package, not an Android/iOS port or a
ready-to-play release. Keep the two application entries together with App,
Runtime, Licenses and manifest.json.

Linux: OpenMU-Game launches the game; OpenMU-GM starts the existing web GM
administration system in your default browser.
macOS: use the corresponding OpenMU-Game.app and OpenMU-GM.app entries.
The GM interface is browser-based, not a native mobile GM application.

The desktop entries open a graphical launcher with first-run administrator
setup, start, backup and graceful stop controls. Choose an administrator
password of at least 12 characters on first launch. The two entries use the
same saved data. Closing a launcher does not stop a running local service.

Optional terminal setup:
  App/GMHost/OpenMU-GM --root /absolute/package/path --setup
Choose the localadmin password, then open either application.
The server and PostgreSQL are private to the current user and use loopback.
No public server, subscription, external payment or test account is used.

Linux saves: per-user local application data / OpenMU-Solo.
macOS saves: ~/Library/Application Support/OpenMU-Solo.
Both application entries share these saves and must use the same package.
Unix credentials use owner-only file permissions, not DPAPI encryption.
Treat backups as private: they contain saved accounts and credentials.

Commands:
  App/GMHost/OpenMU-GM --root /absolute/package/path --backup
  App/GMHost/OpenMU-GM --root /absolute/package/path --stop
  App/GMHost/OpenMU-GM --root /absolute/package/path --verify

Closing the game or browser leaves the local server running. Stop it with
the --stop command for a graceful save and automatic database backup.
If a required port is occupied, the launcher refuses to stop another service.

Remaining release requirements:
- Native graphical launcher acceptance.
- Native graphics, audio, input, installation and dependency verification.
- Native Rime runtime and its dependencies for controller pinyin input.
- Actual monster/mount frame-rate and 117 controller workflow acceptance.
- Physical controller motor feedback and a complete solo progression run.
- Signed/notarized macOS distribution on a Mac.
- Mobile renderer, touch UI, embedded persistent server and mobile packaging.
