# -*- coding: utf-8 -*-
"""Central path configuration for the workspace tooling.

Every script that needs a filesystem location should take it from here rather
than embedding an absolute path, so that the workspace or the installed server
can be moved without touching dozens of files.

Workspace-relative paths are always safe to read at import time. Anything under
the installed server tree is resolved lazily, because a tool that only touches
the workspace should not fail just because the server is not installed.

The installed server root defaults to C:\\OpenMU-Local and can be overridden
with the MU_SERVER_ROOT environment variable (used by the provisioning scripts
and by anything that writes to the live game tree).
"""

import os

# This file lives at the workspace root.
WORKSPACE = os.path.dirname(os.path.abspath(__file__))

MU_MAIN = os.path.join(WORKSPACE, 'MuMain')
MU_MAIN_SRC = os.path.join(MU_MAIN, 'src')
LOCALIZATION = os.path.join(MU_MAIN_SRC, 'Localization')
# Dev tree written by the MuMain build.
DEV_TREE = os.path.join(MU_MAIN_SRC, 'bin')
BUILD_OUT = os.path.join(MU_MAIN, 'out', 'build', 'windows-x86')
MAIN_EXE = os.path.join(BUILD_OUT, 'src', 'Release', 'Main.exe')
GENERATED_I18N = os.path.join(BUILD_OUT, 'Generated', 'I18N')

LOC = os.path.join(WORKSPACE, 'loc')
LOC_CHUNKS = os.path.join(LOC, 'chunks')
LOC_OUT = os.path.join(LOC, 'out')
STRINGS_JSON = os.path.join(LOC, 'strings.json')
LOCS_JSON = os.path.join(LOC, 'locs.json')

LOCWORK = os.path.join(WORKSPACE, '_locwork')
ZHWORK = os.path.join(WORKSPACE, '_zhwork')
PUBLISH = os.path.join(WORKSPACE, '发布包')

DEFAULT_SERVER_ROOT = r'C:\OpenMU-Local'
_SERVER_ROOT = None


def server_root():
    """Installed server/game root, honoring the MU_SERVER_ROOT override."""
    global _SERVER_ROOT
    if _SERVER_ROOT is None:
        root = os.environ.get('MU_SERVER_ROOT') or DEFAULT_SERVER_ROOT
        if not os.path.isdir(root):
            raise SystemExit(
                f'server root not found: {root} '
                '(set the MU_SERVER_ROOT environment variable to override)'
            )
        _SERVER_ROOT = root
    return _SERVER_ROOT


class _Paths:
    """Server-tree paths, resolved on first attribute use."""

    def __init__(self, root):
        self._root = root

    @property
    def root(self):
        return self._root

    @property
    def game(self):
        return os.path.join(self._root, 'App', 'Game')

    @property
    def manifest(self):
        return os.path.join(self._root, 'manifest.json')

    @property
    def chs(self):
        return os.path.join(self.game, 'Data', 'Local', 'Chs')

    @property
    def eng(self):
        return os.path.join(self.game, 'Data', 'Local', 'Eng')

    @property
    def server_app(self):
        return os.path.join(self._root, 'App', 'Server')

    @property
    def keys(self):
        return os.path.join(self._root, 'Data', 'Keys')


class _ServerProxy:
    """Lazy stand-in for the server tree: use config.SERVER.chs etc."""

    def __getattr__(self, item):
        if item.startswith('_'):
            raise AttributeError(item)
        return getattr(_Paths(server_root()), item)

    # config.SERVER() -> the bare root, for scripts that want a single path.
    def __call__(self):
        return server_root()


SERVER = _ServerProxy()
