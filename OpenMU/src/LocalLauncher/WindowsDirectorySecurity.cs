// <copyright file="WindowsDirectorySecurity.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

/// <summary>
/// Applies a private current-user ACL to launcher data.
/// </summary>
public static class WindowsDirectorySecurity
{
    private const uint FilePersistentAcls = 0x00000008;

    /// <summary>
    /// Replaces inherited permissions with full control for the current user and Local System.
    /// </summary>
    /// <param name="directoryPath">The existing directory to protect.</param>
    public static void RestrictToCurrentUser(string directoryPath)
    {
        EnsurePersistentAclsSupported(directoryPath);
        var currentUser = WindowsIdentity.GetCurrent().User
                          ?? throw new InvalidOperationException("当前 Windows 用户没有可用的安全标识符。");
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directoryPath).SetAccessControl(security);
    }

    private static void EnsurePersistentAclsSupported(string directoryPath)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        var volumeRoot = Path.GetPathRoot(fullPath)
                         ?? throw new InvalidOperationException($"无法确定数据目录所在的卷：{fullPath}");
        var fileSystemName = new StringBuilder(64);
        if (!GetVolumeInformation(
                volumeRoot,
                null,
                0,
                out _,
                out _,
                out var fileSystemFlags,
                fileSystemName,
                fileSystemName.Capacity))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法检查数据目录所在卷的安全能力：{volumeRoot}");
        }

        if ((fileSystemFlags & FilePersistentAcls) == 0)
        {
            var format = fileSystemName.Length == 0 ? "未知文件系统" : fileSystemName.ToString();
            throw new NotSupportedException(
                $"OpenMU 本地版需要支持 Windows ACL 的卷来保护数据库和密钥。当前卷为 {format}（{volumeRoot}），请将完整程序包解压到 NTFS 或其他支持持久 ACL 的卷后再运行。");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);
}
