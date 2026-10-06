// <copyright file="ConnectionManager.ClientToServer.Custom.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.Client.Library;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Xor;

/// <summary>
/// Extension methods to start writing messages of this namespace on a <see cref="IConnection"/>.
/// </summary>
public unsafe partial class ConnectionManager
{
    private static readonly Xor3Encryptor Xor3Encryptor = new(0);

    /// <summary>
    /// Sends a <see cref="LoginLongPassword" /> to this connection.
    /// </summary>
    /// <param name="handle">The handle of the connection.</param>
    /// <param name="username">The user name, "encrypted" with Xor3.</param>
    /// <param name="password">The password, "encrypted" with Xor3.</param>
    /// <param name="tickCount">The tick count.</param>
    /// <param name="clientVersion">The client version.</param>
    /// <param name="clientSerial">The client serial.</param>
    /// <remarks>
    /// Is sent by the client when: The player tries to log into the game.
    /// Causes reaction on server side: The server is authenticating the sent login name and password. If it's correct, the state of the player is proceeding to be logged in.
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "ConnectionManager_SendLogin")]
    public static void SendLogin(int handle, IntPtr username, IntPtr password, uint @tickCount, byte* @clientVersion, byte* @clientSerial)
    {
        if (!Connections.TryGetValue(handle, out var connection))
        {
            return;
        }

        try
        {
            var usernameStr = NativeInterop.PtrToWideString(@username);
            var passwordStr = NativeInterop.PtrToWideString(@password);

            if (usernameStr is null)
            {
                throw new ArgumentNullException(nameof(username));
            }

            if (passwordStr is null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            // PLAT-4: the wire fields hold 10/20 UTF-8 bytes (not wchar counts),
            // so a CJK/accented name or anything longer cannot be sent. The old
            // code let GetBytes throw inside the CreateAndSend lambda and the
            // empty catch swallowed it, so clicking Login did nothing at all.
            // Validate up front (same pattern as SendChatMessageExt) and report.
            const int MaximumUsernameBytes = 10;
            const int MaximumPasswordBytes = 20;

            ValidateLoginField(usernameStr, nameof(username), MaximumUsernameBytes);
            ValidateLoginField(passwordStr, nameof(password), MaximumPasswordBytes);

            connection.CreateAndSend(pipeWriter =>
            {
                Span<byte> usernameBytes = stackalloc byte[MaximumUsernameBytes];
                Span<byte> passwordBytes = stackalloc byte[MaximumPasswordBytes];
                Encoding.UTF8.GetBytes(usernameStr, usernameBytes);
                Encoding.UTF8.GetBytes(passwordStr, passwordBytes);
                Xor3Encryptor.Encrypt(usernameBytes);
                Xor3Encryptor.Encrypt(passwordBytes);

                var length = LoginLongPasswordRef.Length;
                var packet = new LoginLongPasswordRef(pipeWriter.GetSpan(length)[..length]);
                usernameBytes.CopyTo(packet.Username);
                passwordBytes.CopyTo(packet.Password);
                packet.TickCount = @tickCount;
                new Span<byte>(@clientVersion, packet.ClientVersion.Length).CopyTo(packet.ClientVersion);
                new Span<byte>(@clientSerial, packet.ClientSerial.Length).CopyTo(packet.ClientSerial);

                return length;
            });
        }
        catch (Exception ex)
        {
            // PLAT-4: at minimum record why the login packet never went out.
            Debug.WriteLine($"Failed to send login request: {ex.Message}");
        }

        // The argument name doubles as the field label, so the exception message
        // wording is identical for both callers.
        static void ValidateLoginField(string value, string argumentName, int maximumBytes)
        {
            var byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount <= 0 || byteCount > maximumBytes)
            {
                throw new ArgumentOutOfRangeException(
                    argumentName,
                    $"The login {argumentName} must contain 1 to {maximumBytes} UTF-8 bytes.");
            }
        }
    }
}
