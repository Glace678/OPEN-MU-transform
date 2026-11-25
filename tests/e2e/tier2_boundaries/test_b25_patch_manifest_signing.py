# tests/e2e/tier2_boundaries/test_b25_patch_manifest_signing.py
"""Regression tests for patch_manifest.py: a signed live manifest must never be left unsigned.

The local launcher verifies the detached signature ``manifest.json.sig`` over the raw manifest
bytes and fails closed when the signature does not match. ``patch_manifest.py`` rewrites the live
manifest, so it has to re-sign it (or refuse before touching it). Before the fix it rewrote the
manifest and left the stale signature in place, which made every later launcher start fail.
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SCRIPT = os.path.join(REPO_ROOT, "patch_manifest.py")
TARGETS = ["App/Game/Main.exe", "App/Game/MUnique.Client.Library.dll"]


class TestPatchManifestSigning(unittest.TestCase):
    """Verifies the signing contract of the live manifest patcher."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="openmu-patch-manifest-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def _make_server_tree(self):
        game = os.path.join(self.root, "App", "Game")
        os.makedirs(game)
        files = []
        for target in TARGETS:
            path = os.path.join(game, os.path.basename(target))
            with open(path, "wb") as handle:
                handle.write(b"new-build-" + target.encode("ascii"))
            files.append({"path": target, "size": 1, "sha256": "0" * 64})
        manifest_path = os.path.join(self.root, "manifest.json")
        with open(manifest_path, "w", encoding="utf-8") as handle:
            json.dump({"formatVersion": 1, "version": "0.0.1", "files": files}, handle, indent=2)
        return manifest_path

    def _run(self):
        env = dict(os.environ)
        env["MU_SERVER_ROOT"] = self.root
        env.pop("OPENMU_MANIFEST_SIGNING_KEY", None)
        return subprocess.run(
            [sys.executable, SCRIPT],
            cwd=REPO_ROOT,
            env=env,
            capture_output=True,
            text=True,
            check=False,
        )

    @staticmethod
    def _read(path, mode="r"):
        if mode == "rb":
            with open(path, "rb") as handle:
                return handle.read()
        with open(path, mode) as handle:
            return handle.read()

    def test_signed_manifest_is_not_rewritten_without_signing_key(self):
        manifest_path = self._make_server_tree()
        signature_path = manifest_path + ".sig"
        with open(signature_path, "w", encoding="ascii") as handle:
            handle.write("c2lnbmF0dXJl")
        before_manifest = self._read(manifest_path, "rb")
        before_signature = self._read(signature_path)

        result = self._run()

        self.assertNotEqual(0, result.returncode, result.stderr)
        self.assertIn("OPENMU_MANIFEST_SIGNING_KEY", result.stderr)
        self.assertEqual(before_manifest, self._read(manifest_path, "rb"))
        self.assertEqual(before_signature, self._read(signature_path))
        self.assertFalse(os.path.exists(manifest_path + ".tmp"))

    def test_unsigned_manifest_is_patched_and_stays_unsigned(self):
        manifest_path = self._make_server_tree()

        result = self._run()

        self.assertEqual(0, result.returncode, result.stderr)
        patched = json.loads(self._read(manifest_path, "r"))
        for entry in patched["files"]:
            target = os.path.join(self.root, *entry["path"].split("/"))
            self.assertEqual(os.path.getsize(target), entry["size"])
            self.assertEqual(64, len(entry["sha256"]))
            self.assertNotEqual("0" * 64, entry["sha256"])
        self.assertFalse(os.path.exists(manifest_path + ".sig"))


if __name__ == "__main__":
    unittest.main()
