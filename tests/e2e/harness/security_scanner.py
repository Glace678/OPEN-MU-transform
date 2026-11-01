# tests/e2e/harness/security_scanner.py
"""
OpenMU & MuMain Security and Deployment Audit Scanner.
Scans release packages for plaintext credentials, validates PowerShell 5.1 compatibility,
and verifies script error fixes and secret lifecycle management.
"""

import os
import re
import ast
import xml.etree.ElementTree as ET
from typing import List, Dict, Tuple


KNOWN_LEAKED_PASSWORDS = [
    "Password=admin",
    "Password=config",
    "Password=account",
    "Password=friend",
    "Password=guild",
    "admin:openmu",
]


class SecurityScanner:
    @staticmethod
    def audit_connection_settings_xml(file_path: str) -> Dict[str, any]:
        """
        Feature 23: Audits ConnectionSettings.xml for plaintext passwords.
        """
        if not os.path.exists(file_path):
            return {"exists": False, "leaks": [], "is_clean": False, "error": f"File not found: {file_path}"}

        try:
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()

            leaks = []
            for pwd_pattern in KNOWN_LEAKED_PASSWORDS:
                if pwd_pattern.lower() in content.lower():
                    leaks.append(pwd_pattern)

            # Check if well-formed XML
            root = ET.fromstring(content)
            connections = root.findall(".//{http://www.munique.net/ConnectionSettings}Connection")
            if not connections:
                # Try without namespace
                connections = root.findall(".//Connection")

            return {
                "exists": True,
                "is_well_formed_xml": True,
                "connection_count": len(connections),
                "leaks": leaks,
                "is_clean": len(leaks) == 0,
            }
        except Exception as e:
            return {"exists": True, "leaks": [], "is_clean": False, "error": str(e)}

    @staticmethod
    def audit_quickstart_powershell_compatibility(file_path: str) -> Dict[str, any]:
        """
        Feature 24: Audits quick-start.ps1 for Windows PowerShell 5.1 compatibility.
        Detects whether [RandomNumberGenerator]::GetInt32 is avoided in favor of PS 5.1 compatible API.
        """
        if not os.path.exists(file_path):
            return {"exists": False, "has_ps51_incompatibility": False, "error": f"File not found: {file_path}"}

        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        # Check for [RandomNumberGenerator]::GetInt32 call
        has_getint32 = bool(re.search(r'\[.*RandomNumberGenerator.*\]::GetInt32', content, re.IGNORECASE))
        has_getbytes = bool(re.search(r'\.GetBytes\(', content, re.IGNORECASE))
        has_get_random = bool(re.search(r'Get-Random', content, re.IGNORECASE))

        is_ps51_compatible = not has_getint32 and (has_getbytes or has_get_random)

        return {
            "exists": True,
            "has_getint32": has_getint32,
            "has_getbytes": has_getbytes,
            "has_get_random": has_get_random,
            "is_ps51_compatible": is_ps51_compatible,
        }

    @staticmethod
    def audit_python_script_imports(file_path: str) -> Dict[str, any]:
        """
        Feature 25: Audits python script (e.g. loc/verify_02.py) for missing imports and syntax validity.
        """
        if not os.path.exists(file_path):
            return {"exists": False, "syntax_valid": False, "has_os_import": False, "error": f"File not found: {file_path}"}

        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        # Syntax check via AST
        try:
            tree = ast.parse(content, filename=file_path)
            syntax_valid = True
        except SyntaxError as e:
            return {"exists": True, "syntax_valid": False, "has_os_import": False, "error": str(e)}

        # Check if 'os' is imported
        imports = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    imports.append(alias.asname or alias.name)
            elif isinstance(node, ast.ImportFrom):
                if node.module:
                    imports.append(node.module)

        has_os_import = "os" in imports

        return {
            "exists": True,
            "syntax_valid": syntax_valid,
            "imports": imports,
            "has_os_import": has_os_import,
        }

    @staticmethod
    def audit_sidecar_credentials_lifecycle(file_path: str) -> Dict[str, any]:
        """
        Feature 25: Audits start_client.ps1 for sidecar credentials cleanup in try/finally blocks.
        """
        if not os.path.exists(file_path):
            return {"exists": False, "has_finally_cleanup": False, "error": f"File not found: {file_path}"}

        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        has_finally = "finally" in content.lower()
        has_cleanup = bool(re.search(r'Remove-Item|del\b|Dispose', content, re.IGNORECASE))

        return {
            "exists": True,
            "has_finally_cleanup": has_finally and has_cleanup,
        }
