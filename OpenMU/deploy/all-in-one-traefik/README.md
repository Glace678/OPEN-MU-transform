# All-in-one deployment with Traefik

The compose files in this folder are documented on the documentation website:

* [All-in-one with Traefik](../../docs-website/docs/deployment/all-in-one-traefik.md)
  — the routing labels, the docker network, HTTPS, and the note that Traefik has
  to be restarted after an admin panel user was added
* [Deployment overview](../../docs-website/docs/deployment/overview.md) — how
  this variant compares to the others

Short version, for a local test:

```bash
docker network create proxy
docker compose -f docker-compose.yml up -d
```

The admin panel is then available at http://admin.docker.localhost/.

**Security (84-05 / 75-01):**
- `OPENMU_ADMIN_USER` and `OPENMU_ADMIN_PASSWORD` are **required**; compose
  refuses to start when they are unset. The panel can never be reached
  anonymously. Run `deploy/quick-start.sh` first to generate them into `.env`.
- In the production variant (`docker-compose.prod.yml`) the admin panel router
  is restricted to private source IPs (loopback + RFC1918) via the
  `admin-allowlist@file` middleware in `data-traefik/configurations/dynamic.yml`.
  Public-internet visitors get 403. To expose it publicly, put it behind a
  separate reverse proxy with its own auth and adjust the allowlist.
- Database passwords are injected from the environment (`OPENMU_DB_*_PASSWORD`,
  see `.env.example`). `ConnectionSettings.xml` no longer contains plaintext
  passwords; missing variables cause startup to fail fast.

For the production variant (`docker-compose.prod.yml`), make sure the ACME
storage exists as a file (otherwise Docker creates it as a directory and Let's
Encrypt breaks):

```bash
cp data-traefik/acme.example.json data-traefik/acme.json
chmod 600 data-traefik/acme.json
```