# Private address policy (single source of truth)

The mobile clients, the packaging validator, and the server share one rule for
which server addresses the game traffic and the GM API may talk to. This file is
the reference; every implementation below is tested against the same vectors.

## The rule

* Plain `http:` is only accepted for `localhost`, `127.0.0.1`, link-local
  (`169.254.0.0/16`) and RFC1918 private IPv4 (`10/8`, `172.16/12`, `192.168/16`).
* `https:` is accepted for any host.
* IPv4 literals must be canonical: exactly four decimal octets, each `0-255`, no
  leading zeros, no hex/octal spellings. Other notations (`127.1`, `0x7f000001`,
  `0177.0.0.1`) are rejected even though a URL parser might normalize them.
* Everything else (public hosts over cleartext, credentials in the URL, query
  strings, fragments, non-HTTP schemes) is rejected.

## Test vectors

| Input | http | https | Note |
|---|---|---|---|
| `localhost` | allow | allow | |
| `127.0.0.1` | allow | allow | |
| `127.0.0.2` | allow | allow | whole loopback /8 |
| `10.0.2.2` | allow | allow | |
| `172.16.1.1` | allow | allow | lower bound of 172.16/12 |
| `172.31.255.254` | allow | allow | upper bound of 172.16/12 |
| `172.32.0.1` | deny | allow | just outside the private range |
| `192.168.1.2` | allow | allow | |
| `169.254.1.2` | allow | allow | link-local |
| `169.253.1.2` | deny | allow | outside link-local |
| `8.8.8.8` | deny | allow | public |
| `example.com` | deny | allow | |
| `010.1.1.1` | deny | allow | leading zero |
| `192.168.001.2` | deny | allow | leading zero |
| `192.168.1.999` | deny | allow | octet > 255 |
| `127.1` | deny | allow | short form |
| `0x7f.0.0.1` | deny | allow | hex spelling |
| `0177.0.0.1` | deny | allow | octal spelling |
| `999.999.999.999` | deny | allow | octets out of range |
| `1.2.3` | deny | allow | too few octets |
| `1.2.3.4.5` | deny | allow | too many octets |
| ` 192.168.1.2` (whitespace) | allow | allow | trimmed before validation |
| `user@127.0.0.1` | deny | deny | credentials in the URL |
| `127.0.0.1?x=1` | deny | deny | query string |
| `127.0.0.1#frag` | deny | deny | fragment |
| `file:///test` | deny | deny | non-HTTP scheme |
| `http://` (empty host) | deny | deny | no host |
| `https://` (empty host) | deny | deny | no host |

## Implementations

| Location | Language | Leading zeros | IPv6 |
|---|---|---|---|
| `OpenMU-Android/game-app/.../LocalIpv4Address.java` | Java | rejected | not supported (explicit) |
| `OpenMU-Android/gm-app/.../ServerAddressPolicy.java` | Java | rejected | parses but only lists the first hextet (stricter) |
| `OpenMU-HarmonyOS/build-tools/mobile-pairing.cjs` | JavaScript | rejected | not supported |
| `OpenMU-HarmonyOS/harmony-gm/.../MobileGmClient.ets` | ArkTS | rejected | not supported |
| `OpenMU/src/.../ListenerAddressResolver.cs` | C# | rejected | rejected with a clear error (deliberate) |

`OpenMU-HarmonyOS/build-tools/tests/mobile-pairing.test.cjs` runs the vectors
above for the build-time validator. The Android and ArkTS clients cover their
parsers in their own unit tests; when you touch any of the five
implementations, re-check the full vector table.

## Public server mode

`mobile-pairing.cjs` refuses a non-private `DEFAULT_SERVER_ADDRESS` unless
`OPENMU_ALLOW_PUBLIC_SERVER=1` is set (it then prints a loud warning). That
escape hatch exists for cloud deployments; before using it, the game TCP channel
must be TLS-protected and the mobile pairing must be device-level, because the
compiled-in package key is a shared secret.
