# Deploys freshly built AdminPanel/Shared binaries into the installed server tree
# and syncs manifest.json size/sha256 entries. Run while the stack is up;
# the script stops and restarts the launcher itself.
#
# Paths come from config.py (MU_SERVER_ROOT overrides the default install root),
# so moving the workspace or the install does not require editing this file.
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import config  # noqa: E402  (path bootstrap above)

try:
    ROOT = Path(config.SERVER())
    MANIFEST = Path(config.SERVER.manifest)
except FileNotFoundError:
    print(f"cannot find the installed server tree: {os.environ.get('MU_SERVER_ROOT') or config.DEFAULT_SERVER_ROOT}",
          file=sys.stderr)
    print("set MU_SERVER_ROOT to the installed root and run this script again", file=sys.stderr)
    raise SystemExit(2)

SERVER = ROOT / "App" / "Server"
BUILD = Path(os.environ.get("OPENMU_BUILD_DIR", r"D:\openmu自用\OpenMU\bin\Debug"))
LAUNCHER_EXE = ROOT / "OpenMU-Local.exe"
HEALTH_URL = os.environ.get("OPENMU_HEALTH_URL", "http://127.0.0.1:5080/_health")
SERVER_PROCESS_NAMES = ["OpenMU-Local", "MUnique.OpenMU.Startup"]
STOP_TIMEOUT_SECONDS = 30
HEALTH_TIMEOUT_SECONDS = 180

FILES = [
    "MUnique.OpenMU.Web.AdminPanel.dll",
    "MUnique.OpenMU.Web.AdminPanel.pdb",
    "MUnique.OpenMU.Web.Shared.dll",
    "MUnique.OpenMU.Web.Shared.pdb",
]

# Values written only after every step below succeeded; used to roll back.
previous_manifest_bytes = None
backups = {}
# Files that did not exist before this run; rollback must remove them again.
new_files = set()


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd):
    print(">", " ".join(str(part) for part in cmd))
    subprocess.run([str(part) for part in cmd], check=True)


def stop_server():
    """Kill the launcher and wait (bounded) until its processes are really gone."""
    subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "Stop-Process -Name " + ",".join(SERVER_PROCESS_NAMES) + " -Force -ErrorAction SilentlyContinue"],
        check=False)

    deadline = time.monotonic() + STOP_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        still_running = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "Get-Process -Name " + ",".join(SERVER_PROCESS_NAMES) + " -ErrorAction SilentlyContinue"],
            capture_output=True, text=True, check=False).stdout.strip()
        if not still_running:
            return
        time.sleep(1)
    print(f"warning: {SERVER_PROCESS_NAMES} still running after {STOP_TIMEOUT_SECONDS}s", file=sys.stderr)


def wait_for_health():
    """Poll the explicit health probe until it answers 200, so we do not report a false success."""
    deadline = time.monotonic() + HEALTH_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(HEALTH_URL, timeout=5) as response:
                # Only an explicit 200 from the probe counts as healthy: 401/403/404 (broken
                # routing, static assets or proxy misplacement) must trigger the rollback.
                if response.status == 200:
                    return True
        except (urllib.error.URLError, urllib.error.HTTPError, OSError):
            # urlopen raises HTTPError on any non-2xx answer, so those are retried like a dead server.
            pass
        time.sleep(2)
    return False


def rollback(step):
    """Undo every completed write, best effort, then restart with the old binaries."""
    print(f"deploy failed during {step}; rolling back", file=sys.stderr)

    # A process started from the new binaries must be stopped before restoring;
    # otherwise it keeps running from the rolled-back (possibly locked) files.
    stack_was_started = step not in {"missing build output", "copying"}
    if stack_was_started:
        stop_server()

    for dst, backup in reversed(list(backups.items())):
        try:
            shutil.copy2(backup, dst)
            print("restored", dst)
        except Exception as error:
            print(f"could not restore {dst} from {backup}: {error}", file=sys.stderr)
    # Remove files this run created where no prior file existed to restore.
    for dst in reversed(list(new_files)):
        try:
            dst.unlink()
            print("removed newly created", dst)
        except Exception as error:
            print(f"could not remove new file {dst}: {error}", file=sys.stderr)
    if previous_manifest_bytes is not None:
        try:
            MANIFEST.write_bytes(previous_manifest_bytes)
            print("restored", MANIFEST)
        except Exception as error:
            print(f"could not restore {MANIFEST}: {error}", file=sys.stderr)

    if stack_was_started:
        try:
            os.startfile(str(LAUNCHER_EXE))
            print("restarted the launcher with the previous binaries")
        except Exception as error:
            print(f"could not restart the previous launcher: {error}", file=sys.stderr)

    raise SystemExit(1)


# 1. Stop the stack.
stop_server()

# 2. Copy the new binaries, keeping a timestamped backup of each existing file.
for name in FILES:
    src = BUILD / name
    dst = SERVER / name
    if not src.exists():
        rollback(f"missing build output {src}")

    stamp = time.strftime("%Y%m%d-%H%M%S")
    if dst.exists():
        backup = dst.with_name(dst.name + f".bak-{stamp}")
        shutil.copy2(dst, backup)
        backups[dst] = backup
    else:
        # Brand-new target: rollback must delete it again (nothing to restore).
        new_files.add(dst)

    try:
        shutil.copy2(src, dst)
    except Exception:
        rollback(f"copying {name}")
    print("copied", name)

# 3. Sync the manifest.
try:
    previous_manifest_bytes = MANIFEST.read_bytes()
    manifest = json.loads(previous_manifest_bytes.decode("utf-8"))
except Exception:
    rollback("reading manifest.json")

stamp = time.strftime("%Y%m%d-%H%M%S")
backup = MANIFEST.with_name(f"manifest.json.bak-{stamp}")
try:
    shutil.copy2(MANIFEST, backup)
    print("manifest backup:", backup.name)
except Exception:
    rollback("backing up manifest.json")

if not isinstance(manifest.get("files"), list):
    rollback("invalid manifest.json: missing 'files' array")

entries = {}
for file_entry in manifest["files"]:
    if not isinstance(file_entry, dict) or "path" not in file_entry:
        rollback("invalid manifest.json: every file entry needs a 'path'")
    entries[file_entry["path"]] = file_entry

try:
    for name in FILES:
        rel = f"App/Server/{name}"
        entry = entries.get(rel)
        if entry is None:
            rollback(f"manifest has no entry for {rel}")
        target = SERVER / name
        entry["size"] = target.stat().st_size
        entry["sha256"] = sha256_of(target)
        print("manifest updated:", rel, entry["size"])
except Exception as error:
    rollback(f"updating manifest entries: {error}")

try:
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
except Exception:
    rollback("writing manifest.json")

# 4. Restart the launcher and wait until it actually serves.
try:
    os.startfile(str(LAUNCHER_EXE))
except Exception:
    rollback("starting OpenMU-Local.exe")
print("launcher started; waiting for the admin panel to answer...")

if not wait_for_health():
    rollback("waiting for the admin panel to become ready")

print("deployed OK: stack is up again")
