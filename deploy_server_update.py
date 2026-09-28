# Deploys freshly built AdminPanel/Shared binaries into C:\OpenMU-Local
# and syncs manifest.json size/sha256 entries. Run while the stack is up;
# the script stops and restarts the launcher itself.
import hashlib
import json
import shutil
import subprocess
import time
from pathlib import Path

ROOT = Path(r"C:\OpenMU-Local")
SERVER = ROOT / "App" / "Server"
BUILD = Path(r"D:\openmu自用\OpenMU\bin\Debug")
MANIFEST = ROOT / "manifest.json"

FILES = [
    "MUnique.OpenMU.Web.AdminPanel.dll",
    "MUnique.OpenMU.Web.AdminPanel.pdb",
    "MUnique.OpenMU.Web.Shared.dll",
    "MUnique.OpenMU.Web.Shared.pdb",
]


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd):
    print(">", " ".join(cmd))
    subprocess.run(cmd, check=True)


# 1. Stop the stack.
subprocess.run(["powershell", "-NoProfile", "-Command",
                "Stop-Process -Name OpenMU-Local,MUnique.OpenMU.Startup -Force -ErrorAction SilentlyContinue"],
               check=False)
time.sleep(3)

# 2. Copy the new binaries.
for name in FILES:
    src = BUILD / name
    dst = SERVER / name
    if not src.exists():
        raise SystemExit(f"missing build output: {src}")
    shutil.copy2(src, dst)
    print("copied", name)

# 3. Sync the manifest.
manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
backup = MANIFEST.with_name("manifest.json.bak-account")
shutil.copy2(MANIFEST, backup)
print("manifest backup:", backup.name)

entries = {f["path"]: f for f in manifest["files"]}
for name in FILES:
    rel = f"App/Server/{name}"
    target = SERVER / name
    entries[rel]["size"] = target.stat().st_size
    entries[rel]["sha256"] = sha256_of(target)
    print("manifest updated:", rel, entries[rel]["size"])

MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

# 4. Restart the launcher.
run(["powershell", "-NoProfile", "-Command",
     f"Start-Process '{ROOT / 'OpenMU-Local.exe'}'"])
print("launcher started; waiting for the stack to come up...")
