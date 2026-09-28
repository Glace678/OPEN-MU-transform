# Local Automatic Login

The OpenMU local launcher skips server selection and the account/password
form, including on first launch. If the client already remembers a game account
and password, it continues using that account and its existing characters.
Otherwise, the local server provisions an installation-specific ordinary game
account and the launcher supplies its credentials only to the game process.
Successful authentication opens the usual character selection screen. Character
creation, selection, saves, and server-side account checks are unchanged.

The launcher enables this using the process-local `MU_LOCAL_AUTO_LOGIN=1` flag.
It is off for a normally launched client. Only a connection to literal
`127.0.0.1`, followed by a game-server address of `127.0.0.1`, can use the saved
or launcher-supplied credentials automatically. Passwords are neither placed on
the command line nor re-saved by this automatic path. The generated game login
is separate from the administrator login and survives restarts and restores
that retain the package's original private keys.

There is one automatic attempt per client launch. If a saved account is missing
its saved password, its credentials cannot be decrypted,
the server is full, the version is incompatible, or authentication fails, the
existing selection, error, and manual login interfaces remain available.
An existing saved account is never silently replaced with an empty account.
This option does not bypass authentication or make an unavailable server playable.

To restore manual login in the local package, set `AutomaticGameLogin` to `false`
in its `Data/Keys/local-settings.json` and restart the launcher.
After upgrading an older package, restart its local service once so the new
first-run account is provisioned. The game account is not a GM account.

## Manual Login During Resizing

Resizing the login screen, including a mobile keyboard changing the available
screen height, keeps the account and password already entered and both remember
checkboxes. The active login field stays active without requesting the keyboard
again after it was dismissed. This temporary resize state exists only in memory;
resizing never saves a password or changes the player's save-password consent.
Editing credentials still revokes an older saved password as before.

## Account Self-Service

The manual login screen provides Register, Change Password, and Reset Password
buttons on every client platform. They open the server's player account portal
in the system browser; the game does not pass an account name or password in the
browser URL. Configure `AccountPortalUrl` in the `[CONNECTION SETTINGS]` section
of `config.ini` with the
portal origin, for example `https://accounts.example.com`. The process-local
`MU_ACCOUNT_PORTAL_URL` overrides it. An empty value leaves self-service
unconfigured and shows an error when a player presses one of these buttons.
Do not use the game-server port as the portal address.

Remote portals require HTTPS. Local HTTP is accepted only for literal loopback
addresses; Android's explicitly enabled trusted local pairing also permits
private IPv4 addresses. On Android, use the Account settings beside the launcher's
Settings button to choose the portal address and language. The desktop portal
uses the game's UI language unless `MU_ACCOUNT_PORTAL_CULTURE` is supplied.
Public HTTPS routes are `/register`, `/change-password`, and `/reset-password`;
local HTTP uses the standalone player page without opening the admin dashboard.

New game accounts receive a recovery code. Keep it privately: resetting a lost
password consumes that code and issues a replacement. An existing account must
sign in with its current password to issue its first recovery code. There is no
password reset using only an account name or an old unverified security code.
The legacy Windows HTTP account forms are restricted to loopback and are not
used by these new buttons. Player registration does not create an administrator
account or grant GM permissions.

## NPC Prices On A Balance Server

With a matching balance-v1 server, NPC purchase prices come from the server's
actual inventory quote, including the number of potions in the offer and the
applicable store tax. No additional Solo setting is needed. The shop checks
the quoted total before buying; insufficient funds show the existing Zen
message. Pending or mismatched quotes cannot purchase and do not display a
misleading free price. Close and reopen the merchant after an unavailable quote.

Original servers and the separate Solo profile retain their existing behavior.
Updating only the server does not update an older client's price display; both
the client and server must contain this feature. This purchase integration does
not change inventory resale, repair or crafting tooltips, and does not add stock
to an already initialized older server configuration.
