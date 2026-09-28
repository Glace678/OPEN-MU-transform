"""Read-only deployment contract tests; requires PyYAML, never Docker or a DB."""

from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

import yaml


DEPLOY = Path(__file__).resolve().parents[1]
SOURCE = DEPLOY.parent / "src"
COMPOSE_DIR = DEPLOY / "all-in-one"


class ComposeLoader(yaml.SafeLoader):
    pass


ComposeLoader.add_constructor("!override", lambda loader, node: loader.construct_sequence(node))


class ComposeContractTests(unittest.TestCase):
    def setUp(self):
        self.base = yaml.safe_load((COMPOSE_DIR / "docker-compose.yml").read_text(encoding="utf-8"))
        self.local = yaml.safe_load((COMPOSE_DIR / "docker-compose.local.yml").read_text(encoding="utf-8"))

    def test_local_build_uses_all_local_source_not_the_upstream_image(self):
        service = self.local["services"]["openmu-startup"]
        self.assertEqual(service["image"], "openmu-selfhosted:local")
        self.assertEqual(service["pull_policy"], "never")
        context = (COMPOSE_DIR / service["build"]["context"]).resolve()
        dockerfile = (context / service["build"]["dockerfile"]).resolve()
        self.assertEqual(context, SOURCE)
        self.assertTrue(dockerfile.is_file())
        text = dockerfile.read_text(encoding="utf-8")
        self.assertIn("COPY . .", text)
        self.assertIn('dotnet publish "Startup/MUnique.OpenMU.Startup.csproj"', text)
        self.assertLess(text.index("COPY . ."), text.index("dotnet publish"))
        ignored = (context / ".dockerignore").read_text(encoding="utf-8").splitlines()
        for path in ("**/.env", "**/bin", "**/obj", "**/node_modules", "**/.git"):
            self.assertIn(path, ignored)

    def test_database_credentials_match_all_application_admin_contexts(self):
        services = self.base["services"]
        application = services["openmu-startup"]["environment"]
        database = services["database"]["environment"]
        self.assertEqual(application["DB_HOST"], "database")
        self.assertEqual(application["DB_ADMIN_USER"], database["POSTGRES_USER"])
        self.assertEqual(application["DB_ADMIN_PW"], database["POSTGRES_PASSWORD"])
        self.assertIn(":?", application["DB_ADMIN_PW"])
        self.assertEqual(database["POSTGRES_DB"], "openmu")
        settings = ET.parse(SOURCE / "Persistence/EntityFramework/ConnectionSettings.xml")
        namespace = {"c": "http://www.munique.net/ConnectionSettings"}
        strings = settings.findall(".//c:ConnectionString", namespace)
        self.assertGreater(len(strings), 1)
        self.assertTrue(all("Database=openmu;" in value.text for value in strings))
        self.assertEqual(application["Database__AssumeExternallyProvisioned"], "true")

    def test_database_is_persistent_and_has_no_host_port(self):
        database = self.base["services"]["database"]
        self.assertNotIn("ports", database)
        self.assertEqual(database["volumes"], ["dbdata:/var/lib/postgresql"])
        self.assertIn("dbdata", self.base["volumes"])
        self.assertIn("pg_isready", database["healthcheck"]["test"][1])
        dependency = self.base["services"]["openmu-startup"]["depends_on"]["database"]
        self.assertEqual(dependency["condition"], "service_healthy")

    def test_advertised_address_and_listener_binding_reach_the_application(self):
        application = self.base["services"]["openmu-startup"]["environment"]
        self.assertEqual(application["RESOLVE_IP"], "${RESOLVE_IP:-127.0.0.1}")
        self.assertEqual(str(application["OPENMU_BIND_ADDRESS"]), "0.0.0.0")
        # Container listeners must bind all container interfaces, even for a local host mapping.
        for mapping in self.base["services"]["openmu-startup"]["ports"]:
            self.assertTrue(mapping.startswith("${OPENMU_GAME_BIND:-127.0.0.1}:"))
            self.assertEqual(*mapping.rsplit(":", 2)[1:])

    def test_admin_has_no_direct_unprotected_container_port(self):
        services = self.base["services"]
        self.assertEqual(services["nginx-80"]["ports"], ["127.0.0.1:${OPENMU_WEB_PORT:-80}:80"])
        self.assertFalse(any(mapping.endswith(":8080") for mapping in services["openmu-startup"]["ports"]))
        environment = services["openmu-startup"]["environment"]
        self.assertIn(":?", environment["OPENMU_ADMIN_USER"])
        self.assertIn(":?", environment["OPENMU_ADMIN_PASSWORD"])

    def test_default_startup_does_not_reset_or_discard_saved_progress(self):
        command = self.local["services"]["openmu-startup"]["command"]
        self.assertNotIn("-reinit", command)
        self.assertNotIn("-demo", command)
        self.assertIn("-testaccounts:false", command)
        self.assertIn("-deamon", command)
        # The cloud server's supported game ports match the default initializer contract.
        ports = {int(value.rsplit(":", 1)[1]) for value in self.base["services"]["openmu-startup"]["ports"]}
        self.assertTrue({44405, 44406, 55901, 55902, 55903, 55904, 55905, 55906, 55980}.issubset(ports))

    def test_explicit_https_setup_preserves_the_public_acme_port(self):
        text = (COMPOSE_DIR / "docker-compose.prod.yml").read_text(encoding="utf-8")
        production = yaml.load(text, Loader=ComposeLoader)
        self.assertIn("ports: !override", text)
        self.assertEqual(production["services"]["nginx-80"]["ports"], ["80:80"])

    def test_public_player_portal_leaves_admin_and_database_private(self):
        portal = yaml.safe_load((COMPOSE_DIR / "docker-compose.portal.yml").read_text(encoding="utf-8"))
        service = portal["services"]["player-portal"]
        self.assertEqual(service["ports"], ["443:443"])
        self.assertEqual(set(portal["services"]), {"player-portal"})
        root = (COMPOSE_DIR / service["volumes"][1].split(":")[0]).resolve()
        self.assertEqual(root, SOURCE / "Web/AdminPanel/wwwroot/player-portal")
        self.assertTrue((root / "inventory_back.png").is_file())
        config = (COMPOSE_DIR / "Caddyfile.portal").read_text(encoding="utf-8")
        self.assertIn("disable_http_challenge", config)
        self.assertIn("auto_https disable_redirects", config)
        self.assertIn('respond "Not found" 404', config)
        self.assertIn("header_up -Cookie", config)
        self.assertIn("header_up -Authorization", config)
        self.assertNotIn("/_blazor", config)
        self.assertNotIn("/api/*", config)
        self.assertNotIn("/auth", config)
        self.assertNotIn("/login", config)


if __name__ == "__main__":
    unittest.main()
