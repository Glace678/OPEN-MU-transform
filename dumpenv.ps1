# Dumps the process environment block of a running target.
#
# Used to verify that the launcher-sidecar variables (MU_LOCAL_GAME_USERNAME etc.)
# actually reach the client process. The PEB layout depends on the *target's*
# bitness, not the host's: pass -Bits 32 for the x86 game client, -Bits 64 for a
# 64-bit process. Defaults to the host process bitness.
#
# The host must match the target's bitness: a 64-bit host cannot walk a WOW64
# process this way (that needs the NtWow64 query path, which the originals
# explicitly punted on -- "simplest: run as x86 powershell"). Run from a
# sysnative/x86 PowerShell accordingly.
#
# Merged from _dumpenv2.ps1 (x86) and _dumpenv3.ps1 (x64); _dumpenv.ps1 was the
# broken first attempt (read the PEB pointer with the host's pointer width and
# ignored WOW64) and is not reproduced here.
param(
    [Parameter(Mandatory = $true)]
    [int] $TargetPid,
    [ValidateSet('32', '64')]
    [string] $Bits = ([IntPtr]::Size * 8).ToString()
)

$cs = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class EnvDump
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct PROCESS_BASIC_INFORMATION32
    {
        public uint Reserved1; public uint PebBaseAddress;
        public uint Reserved2_0; public uint Reserved2_1;
        public uint UniqueProcessId; public uint Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_BASIC_INFORMATION64
    {
        public ulong Reserved1; public ulong PebBaseAddress;
        public ulong Reserved2_0; public ulong Reserved2_1;
        public ulong UniqueProcessId; public ulong Reserved3;
    }

    [DllImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
    static extern int NtQueryInformationProcess32(IntPtr h, int c, ref PROCESS_BASIC_INFORMATION32 pbi, int len, out int ret);
    [DllImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
    static extern int NtQueryInformationProcess64(IntPtr h, int c, ref PROCESS_BASIC_INFORMATION64 pbi, int len, out int ret);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr h, ulong addr, byte[] buf, int size, out int read);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);

    // VM_READ | QUERY_INFORMATION | QUERY_LIMITED_INFORMATION
    const uint Access = 0x0010 | 0x0400 | 0x1000;

    static string ReadEnv(IntPtr h, ulong env, int size, StringBuilder sb)
    {
        byte[] envb = new byte[size > 0 && size <= 0x10000 ? size : 0x8000];
        int read;
        if (!ReadProcessMemory(h, env, envb, envb.Length, out read))
            return "RPM_ENV_FAIL " + Marshal.GetLastWin32Error();
        sb.AppendLine("ENVBEGIN");
        sb.Append(Encoding.Unicode.GetString(envb, 0, read));
        sb.AppendLine("ENVEND");
        return sb.ToString();
    }

    // x64: RTL_USER_PROCESS_PARAMETERS.Environment sits at 0x80 in current builds,
    // but has moved over Windows versions, so probe the plausible range and keep
    // whichever pointer actually resolves to an environment block.
    //
    // Do NOT identify the block by searching for "SystemRoot" -- the original
    // script did, and that heuristic fails both ways: a long command line can
    // contain that literal (false positive on CommandLine.Buffer at 0x78), and
    // a process with many variables can push SystemRoot past the read window
    // (false negative on the real block -- observed on this machine's PowerShell,
    // whose block exceeds 64KB). Instead test the region structurally: an
    // environment block is a sequence of "NAME=VALUE\0" strings ending in a
    // bare NUL, and every name is printable ASCII.
    static bool LooksLikeEnvBlock(string text, out int blockChars)
    {
        int start = 0;
        int vars = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\0', start);
            if (end < 0)
                break; // window ended mid-string; block is larger than the read
            if (end == start)
            {
                blockChars = start; // terminating empty entry -> end of block
                return vars >= 8;
            }
            string entry = text.Substring(start, end - start);
            int eq = entry.IndexOf('=');
            if (eq <= 0)
            {
                blockChars = start;
                return false;
            }
            for (int i = 0; i < eq; i++)
                if (entry[i] < ' ' || entry[i] > '~')
                {
                    blockChars = start;
                    return false;
                }
            vars++;
            start = end + 1;
        }
        blockChars = start;
        return vars >= 8; // no terminator in view, but everything seen was clean
    }

    static string Dump64(IntPtr h, ulong peb, StringBuilder sb)
    {
        sb.AppendLine("PEB64=0x" + peb.ToString("X"));
        byte[] buf = new byte[8];
        int read;
        if (!ReadProcessMemory(h, peb + 0x20, buf, buf.Length, out read))
            return "RPM1_FAIL " + Marshal.GetLastWin32Error();
        ulong pp = BitConverter.ToUInt64(buf, 0);
        sb.AppendLine("ProcParams64=0x" + pp.ToString("X"));
        byte[] ppb = new byte[0x4000];
        if (!ReadProcessMemory(h, pp, ppb, ppb.Length, out read))
            return "RPM2_FAIL " + Marshal.GetLastWin32Error();
        for (int off = 0x70; off <= 0x98; off += 8)
        {
            ulong cand = BitConverter.ToUInt64(ppb, off);
            if (cand < 0x10000 || cand > 0x7FFFFFFFFFFF) continue;
            byte[] pb = new byte[0x10000];
            if (!ReadProcessMemory(h, cand, pb, pb.Length, out read)) continue;
            int blockChars;
            if (!LooksLikeEnvBlock(Encoding.Unicode.GetString(pb, 0, read), out blockChars)) continue;
            sb.AppendLine("ENV_CANDIDATE param_off=0x" + off.ToString("X"));
            sb.AppendLine("ENVBEGIN");
            sb.Append(Encoding.Unicode.GetString(pb, 0, blockChars > 0 ? blockChars : read));
            sb.AppendLine("ENVEND");
            return sb.ToString();
        }
        return "NO_ENV_CANDIDATE";
    }

    // x86: ProcessParameters pointer at PEB+0x20, then Environment at 0x44 and
    // EnvironmentSize at 0x48 within RTL_USER_PROCESS_PARAMETERS.
    static string Dump32(IntPtr h, uint peb, StringBuilder sb)
    {
        sb.AppendLine("PEB32=0x" + peb.ToString("X"));
        byte[] buf = new byte[4];
        int read;
        if (!ReadProcessMemory(h, peb + 0x20, buf, buf.Length, out read))
            return "RPM1_FAIL " + Marshal.GetLastWin32Error();
        uint pp = BitConverter.ToUInt32(buf, 0);
        sb.AppendLine("ProcParams32=0x" + pp.ToString("X"));
        byte[] ppb = new byte[0x2000];
        if (!ReadProcessMemory(h, pp, ppb, ppb.Length, out read))
            return "RPM2_FAIL " + Marshal.GetLastWin32Error() + " got=" + read;
        uint env = BitConverter.ToUInt32(ppb, 0x44);
        int envSize = (int)BitConverter.ToUInt32(ppb, 0x48);
        sb.AppendLine("Env=0x" + env.ToString("X") + " EnvSize=" + envSize);
        return ReadEnv(h, env, envSize, sb);
    }

    public static string Dump(int pid, int bits)
    {
        IntPtr h = OpenProcess(Access, false, pid);
        if (h == IntPtr.Zero) return "OPEN_FAIL " + Marshal.GetLastWin32Error();
        try
        {
            int ret;
            var sb = new StringBuilder();
            sb.AppendLine("PTR_SIZE=" + bits);
            if (bits == 32)
            {
                var pbi = new PROCESS_BASIC_INFORMATION32();
                int st = NtQueryInformationProcess32(h, 0, ref pbi, Marshal.SizeOf(pbi), out ret);
                if (st != 0) return "NQIP_FAIL " + st;
                return Dump32(h, pbi.PebBaseAddress, sb);
            }
            else
            {
                var pbi = new PROCESS_BASIC_INFORMATION64();
                int st = NtQueryInformationProcess64(h, 0, ref pbi, Marshal.SizeOf(pbi), out ret);
                if (st != 0) return "NQIP_FAIL " + st;
                return Dump64(h, pbi.PebBaseAddress, sb);
            }
        }
        finally { CloseHandle(h); }
    }
}
"@
Add-Type -TypeDefinition $cs

$raw = [EnvDump]::Dump($TargetPid, [int]$Bits)
if ($raw -match 'ENVBEGIN') {
    $body = ($raw -split 'ENVBEGIN')[1]
    ($body -split 'ENVEND')[0] -split "`0" |
        Where-Object { $_ -match '^(MU_|OPENMU)' } |
        ForEach-Object { Write-Output $_ }
} else {
    Write-Output $raw
}
