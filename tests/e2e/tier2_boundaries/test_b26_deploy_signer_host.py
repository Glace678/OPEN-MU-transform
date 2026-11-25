# tests/e2e/tier2_boundaries/test_b26_deploy_signer_host.py
"""Regression test for deploy_server_update.py: a missing PowerShell host must fail pre-flight.

The deploy script replaces the live binaries, then re-signs the manifest. ``patch_manifest.py``
accepts both Windows PowerShell 5.1 and PowerShell 7, so a machine may legitimately have only
``powershell``. Before the fix the deploy script hard-coded ``pwsh`` and resolved nothing up
front: on such a host it stopped the server, copied the new binaries and only then failed in the
signing step, leaving the rollback to clean up.
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SCRIPT = os.path.join(REPO_ROOT, "deploy_server_update.py")

FILES = [
    "MUnique.OpenMU.Web.AdminPanel.dll",
    "MUnique.OpenMU.Web.AdminPanel.pdb",
    "MUnique.OpenMU.Web.Shared.dll",
    "MUnique.OpenMU.Web.Shared.pdb",
]


class TestDeploySignerHost(unittest.TestCase):
    """Pre-flight checks of the deploy script for the signing interpreter."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="openmu-deploy-signer-")
        self.addCleanup(shutil.rmtree, self.root, True)
        self.server = os.path.join(self.root, "App", "Server")
        os.makedirs(self.server)
        self.build = os.path.join(self.root, "build")
        os.makedirs(self.build)
        entries = []
        for name in FILES:
            with open(os.path.join(self.server, name), "wb") as handle:
                handle.write(b"installed-" + name.encode("ascii"))
            with open(os.path.join(self.build, name), "wb") as handle:
                handle.write(b"fresh-build-" + name.encode("ascii"))
            entries.append({"path": "App/Server/" + name, "size": 1, "sha256": "0" * 64})
        self.manifest = os.path.join(self.root, "manifest.json")
        with open(self.manifest, "w", encoding="utf-8") as handle:
            json.dump({"formatVersion": 1, "files": entries}, handle, indent=2)
        self.signature = self.manifest + ".sig"
        with open(self.signature, "w", encoding="ascii") as handle:
            handle.write("c2lnbmF0dXJl")
        self.key = os.path.join(self.root, "manifest-signing-private.xml")
        with open(self.key, "w", encoding="utf-8") as handle:
            handle.write("<RSAKeyValue><Modulus>x</Modulus><Exponent>y</Exponent></RSAKeyValue>")

    def _run(self, path_env):
        env = dict(os.environ)
        env["MU_SERVER_ROOT"] = self.root
        env["OPENMU_BUILD_DIR"] = self.build
        env["OPENMU_MANIFEST_SIGNING_KEY"] = self.key
        env["PATH"] = path_env
        env.pop("OPENMU_HEALTH_URL", None)
        return subprocess.run(
            [sys.executable, SCRIPT],
            cwd=REPO_ROOT,
            env=env,
            capture_output=True,
            text=True,
            check=False,
        )

    @staticmethod
    def _read(path):
        with open(path, "rb") as handle:
            return handle.read()

    def test_missing_powershell_host_fails_before_touching_the_tree(self):
        before_manifest = self._read(self.manifest)
        before_signature = self._read(self.signature)
        before_binaries = {name: self._read(os.path.join(self.server, name)) for name in FILES}

        empty_path = os.path.join(self.root, "empty-path")
        os.makedirs(empty_path)
        result = self._run(empty_path)

        self.assertEqual(2, result.returncode, result.stderr)
        self.assertIn("neither pwsh nor powershell", result.stderr.lower())
        self.assertIn("not modified", result.stderr.lower())
        self.assertNotIn("Traceback", result.stderr)
        self.assertEqual(before_manifest, self._read(self.manifest))
        self.assertEqual(before_signature, self._read(self.signature))
        for name in FILES:
            self.assertEqual(
                before_binaries[name],
                self._read(os.path.join(self.server, name)),
                name,
            )
        self.assertFalse(os.path.exists(self.manifest + ".tmp"))


if __name__ == "__main__":
    unittest.main()
