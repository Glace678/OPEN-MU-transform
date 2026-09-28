# All-in-one deployment

The compose files in this folder are documented on the documentation website:

* [All-in-one deployment](../../docs-website/docs/deployment/all-in-one.md) —
  local testing, HTTPS with certbot, and what to do afterwards
* [Deployment overview](../../docs-website/docs/deployment/overview.md) — how
  this variant compares to the others

For this locally modified project, use the one-click script from the OpenMU root:

```bash
bash deploy/quick-start.sh
```

It builds local source and generates a random admin password in `all-in-one/.env`.
The admin panel is loopback-only at `http://127.0.0.1/`; PostgreSQL has no host port.
Do not use the old development override to deploy a cloud server, because that
override adds database and direct admin ports.

To add the isolated public player account portal while keeping administration private:

```bash
bash deploy/quick-start.sh --public-address YOUR_PUBLIC_IPV4 --portal-domain accounts.your-domain.com
```

The optional `docker-compose.portal.yml` publishes only TCP 443 and uses a strict
account API allowlist. Do not combine it with the older full-admin production or
Traefik overlays. The portal serves `/register`, `/change-password`, and
`/reset-password`; new registrations receive a private rotating recovery code.

See [the one-click guide](../README.md) for cloud/LAN addressing, SSH access,
read-only dry runs, data preservation, and focused tests.
