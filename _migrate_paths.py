# -*- coding: utf-8 -*-
# One-shot migration: replace hardcoded absolute paths with config.* lookups.
#
# Rules, longest literal first so prefixes never shadow longer matches:
#
#   D:\openmu自用\_locwork / _zhwork   -> own directory (W), since these scripts
#                                       always live in that directory
#   D:\openmu自用\_locwork\...deep    -> os.path.join(W, '<relative>')
#   D:\openmu自用\loc\strings.json    -> config.STRINGS_JSON
#   D:\openmu自用\loc\chunks          -> config.LOC_CHUNKS
#   D:\openmu自用\loc\out             -> config.LOC_OUT
#   D:\openmu自用\loc                 -> config.LOC
#   D:\openmu自用\MuMain\...          -> config.* equivalents
#   D:\openmu自用                     -> config.WORKSPACE
#   C:\Users\Glace\AppData\Local\Temp -> W  (gap scripts imported gap_engine from
#                                       there; it actually lives alongside them)
#   C:\OpenMU-Local\...               -> config.SERVER.* (lazy)
#
# Injects a sys.path bootstrap + `import config` where the result references
# config, and a `W =` definition where a rewritten expression uses W without one.

import ast
import os
import re

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
WORKSPACE_LIT = r'D:\openmu自用'
SERVER_LIT = r'C:\OpenMU-Local'
TEMP_LIT = r'C:\Users\Glace\AppData\Local\Temp'

# Workspace literals -> replacement expression, longest first.
WORKSPACE_RULES = [
    (r'\loc\strings.json', 'config.STRINGS_JSON'),
    (r'\loc\chunks', 'config.LOC_CHUNKS'),
    (r'\loc\out', 'config.LOC_OUT'),
    (r'\loc', 'config.LOC'),
    (r'\MuMain\src\Localization', 'config.LOCALIZATION'),
    (r'\MuMain\out\build\windows-x86\src\Release\Main.exe', 'config.MAIN_EXE'),
    (r'\MuMain\out\build\windows-x86\Generated\I18N', 'config.GENERATED_I18N'),
    (r'\MuMain\out\build\windows-x86', 'config.BUILD_OUT'),
    (r'\MuMain\src\bin', 'config.DEV_TREE'),
    (r'\MuMain\src', 'config.MU_MAIN_SRC'),
    (r'\MuMain', 'config.MU_MAIN'),
    (r'\发布包', 'config.PUBLISH'),
    ('', 'config.WORKSPACE'),
]

SERVER_RULES = [
    (r'\manifest.json', 'config.SERVER.manifest'),
    (r'\App\Game\Data\Local\Chs', 'config.SERVER.chs'),
    (r'\App\Game\Data\Local\Eng', 'config.SERVER.eng'),
    (r'\App\Game', 'config.SERVER.game'),
    ('', 'config.SERVER()'),
]

STRING_RE = re.compile(r'''([rbuRBU]{0,2})(['"])((?:D:\\openmu[^'"]*|C:\\OpenMU-Local[^'"]*|C:\\Users\\Glace\\AppData\\Local\\Temp))\2''')


def _join(expr, rest):
    """os.path.join(expr, 'a', 'b', ...) for a backslash-separated tail."""
    parts = [p for p in rest.split('\\') if p]
    if not parts:
        return expr
    args = ', '.join("'%s'" % p for p in parts)
    return 'os.path.join(%s, %s)' % (expr, args)


def _lookup(rules, rest):
    """Longest-prefix match on a rule table; suffixes are component-aligned.

    Each rule suffix starts with a backslash except the catch-all ''. A match is
    either exact or a full component longer, so the LOC_OUT rule never swallows a
    deeper "loc/output" path.
    """
    for suffix, expr in rules:
        if rest == suffix:
            return expr
        if suffix and rest.startswith(suffix + '\\'):
            return _join(expr, rest[len(suffix):])
    return None


def replacement(path):
    """Return the Python expression replacing the literal `path`, or None."""
    if path == TEMP_LIT:
        return 'W'
    if path.startswith(WORKSPACE_LIT):
        rest = path[len(WORKSPACE_LIT):]
        own_dir = os.path.join(WORKSPACE, '_locwork'), os.path.join(WORKSPACE, '_zhwork')
        for d in own_dir:
            if path == d:
                return 'W'
            if path.startswith(d + '\\'):
                return _join('W', path[len(d) + 1:])
        return _lookup(WORKSPACE_RULES, rest)
    if path.startswith(SERVER_LIT):
        return _lookup(SERVER_RULES, path[len(SERVER_LIT):])
    return None


def rewrite_source(src, own_subdir):
    """Apply literal substitutions; returns (new_src, uses_config, uses_W)."""
    uses_config = [False]
    uses_W = [False]

    def sub(m):
        prefix, quote, path = m.group(1), m.group(2), m.group(3)
        expr = replacement(path)
        if expr is None:
            return m.group(0)
        # Nested forms like os.path.join(config.X, ...) do not start with 'config'.
        if 'config' in expr:
            uses_config[0] = True
        if 'W' in expr:
            uses_W[0] = True
        return expr

    out = STRING_RE.sub(sub, src)
    # A file that defined `W = r'...its own dir...'` now reads `W = W`. The
    # bootstrap supplies that definition, so drop the tautology -- and it must
    # go before module_level_names(), or "W" would look already defined.
    if uses_W[0]:
        out = re.sub(r'(?m)^W[ \t]*=[ \t]*W[ \t]*\r?\n', '', out)
    return out, uses_config[0], uses_W[0]


def module_level_names(tree):
    """Names assigned or imported at module level."""
    names = set()
    for node in tree.body:
        if isinstance(node, (ast.Import, ast.ImportFrom)):
            for a in node.names:
                names.add((a.asname or a.name).split('.')[0])
        elif isinstance(node, ast.Assign):
            for t in node.targets:
                if isinstance(t, ast.Name):
                    names.add(t.id)
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
            names.add(node.name)
    return names


def bootstrap_for(rel_depth, needs_config, needs_W):
    lines = []
    # The W line uses _os too, so the alias import is needed in both cases.
    if needs_config or needs_W:
        lines.append('import os as _os' + (', sys as _sys' if needs_config else ''))
    if needs_config:
        if rel_depth > 0:
            lines.append(
                '_sys.path.insert(0, _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))'
            )
        lines.append('import config')
    if needs_W:
        lines.append('W = _os.path.dirname(_os.path.abspath(__file__))')
    return lines


def migrate(path, apply_changes):
    # newline='' keeps \r\n intact; text-mode translation would rewrite every
    # line in the file.
    with open(path, encoding='utf-8', newline='') as fh:
        src = fh.read()
    rel = os.path.relpath(path, WORKSPACE)
    depth = 0 if os.path.dirname(rel) in ('', '.') else 1
    new_src, uses_config, uses_W = rewrite_source(src, depth)
    if new_src == src:
        return None

    tree = ast.parse(new_src)
    defined = module_level_names(tree)
    needs_import = uses_config and 'config' not in defined
    needs_W = uses_W and 'W' not in defined
    if not needs_import and not needs_W:
        if apply_changes:
            with open(path, 'w', encoding='utf-8', newline='') as fh:
                fh.write(new_src)
        return ('rewritten', rel) if apply_changes else ('would-rewrite', rel)

    lines = bootstrap_for(depth, needs_import, needs_W)
    body = new_src.split('\n')
    # Insert after a coding cookie / shebang, which must stay first.
    insert_at = 0
    for i, line in enumerate(body[:3]):
        if line.startswith('#!') or 'coding:' in line:
            insert_at = i + 1
        else:
            break
    body[insert_at:insert_at] = lines
    new_src = '\n'.join(body)
    ast.parse(new_src)  # safety: still syntactically valid
    if apply_changes:
        with open(path, 'w', encoding='utf-8', newline='') as fh:
            fh.write(new_src)
    return ('rewritten+bootstrapped' if apply_changes else 'would-rewrite+bootstrap', rel)


TARGET_DIRS = ['loc', '_locwork', '_zhwork']
TARGET_FILES = ['patch_manifest.py', 'wakeup_claude.py']

if __name__ == '__main__':
    import sys

    apply_changes = '--apply' in sys.argv
    files = []
    for name in TARGET_FILES:
        p = os.path.join(WORKSPACE, name)
        if os.path.isfile(p):
            files.append(p)
    for sub in TARGET_DIRS:
        d = os.path.join(WORKSPACE, sub)
        if not os.path.isdir(d):
            continue
        for fn in sorted(os.listdir(d)):
            if fn.endswith('.py'):
                files.append(os.path.join(d, fn))
    own = os.path.join(WORKSPACE, '_migrate_paths.py')
    if own in files:
        files.remove(own)

    changed = []
    for p in files:
        try:
            result = migrate(p, apply_changes)
        except SyntaxError as exc:
            print(f'SYNTAX-ERROR {p}: {exc}')
            continue
        if result:
            changed.append((p, result[0]))
    print(f'\n{"APPLIED" if apply_changes else "DRY RUN"}: {len(changed)} files')
    for p, kind in changed:
        print(f'  {kind:22s} {os.path.relpath(p, WORKSPACE)}')
