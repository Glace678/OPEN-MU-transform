// <copyright file="SimpleModulusKeySerializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.SimpleModulus;

using System.Diagnostics.CodeAnalysis;
using System.IO;
using static System.Buffers.Binary.BinaryPrimitives;

/// <summary>
/// Serializer to load/save the simple modulus keys into files in the original format of Webzen.
/// </summary>
public class SimpleModulusKeySerializer
{
    /// <summary>
    /// The xor encryption keys which used to "encrypt" and "decrypt" (XOR) the encryption keys to dat files.
    /// In newer versions, only the first 4 keys are in use.
    /// Older versions use a slightly different SimpleModulus algorithm with 16 keys.
    /// </summary>
    private readonly uint[] _encryptionKeys =
    {
        0x3F08A79B, 0xE25CC287, 0x93D27AB9, 0x20DEA7BF,
        0x837A9BC7, 0x1FFEA89B, 0xE836579C, 0xD89A0924,
        0x9D8A73CF, 0x4DEA98CE, 0x4DC9EF0F, 0x2BAC890A,
        0xF32AFE54, 0xD8902A1E, 0x3FDEF8F9, 0x5A827990,
    };

    /// <summary>
    /// The header for key files which contain just one side of the key (encryption or decryption).
    /// </summary>
    private readonly byte[] _headerOneKey = { 0x12, 0x11 };

    /// <summary>
    /// Serializes (saves) the specified keys to the specified file name.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="modKey">The mod key.</param>
    /// <param name="key">The key.</param>
    /// <param name="xorKey">The xor key.</param>
    public void Serialize(string fileName, uint[] modKey, uint[] key, uint[] xorKey)
    {
        // File.Create truncates any previously existing (possibly longer) file;
        // OpenWrite would leave trailing bytes of the old key file in place.
        using var fileStream = File.Create(fileName);
        var length = (uint)(modKey.Length * sizeof(uint) * 3);
        fileStream.Write(this._headerOneKey, 0, this._headerOneKey.Length);
        var intBuffer = new byte[4];

        WriteUInt32LittleEndian(intBuffer, length);
        fileStream.Write(intBuffer, 0, 4);
        for (int i = 0; i < modKey.Length; i++)
        {
            var value = modKey[i] ^ this._encryptionKeys[i];
            WriteUInt32LittleEndian(intBuffer, value);
            fileStream.Write(intBuffer, 0, 4);
        }

        for (int i = 0; i < key.Length; i++)
        {
            var value = key[i] ^ this._encryptionKeys[i];
            WriteUInt32LittleEndian(intBuffer, value);
            fileStream.Write(intBuffer, 0, 4);
        }

        for (int i = 0; i < xorKey.Length; i++)
        {
            var value = xorKey[i] ^ this._encryptionKeys[i];
            WriteUInt32LittleEndian(intBuffer, value);
            fileStream.Write(intBuffer, 0, 4);
        }

        fileStream.Flush(true);
    }

    /// <summary>
    /// Deserializes (loads) the keys from the specified file.
    /// </summary>
    /// <param name="fileName">Name (and path) of the file.</param>
    /// <param name="modulusKey">The modulus key.</param>
    /// <param name="key">The key.</param>
    /// <param name="xorKey">The xor key.</param>
    /// <returns><c>True</c>, if successful; Otherwise, <c>false</c>.</returns>
    public bool TryDeserialize(
        string fileName,
        [MaybeNullWhen(false)] out uint[] modulusKey,
        [MaybeNullWhen(false)] out uint[] key,
        [MaybeNullWhen(false)] out uint[] xorKey)
    {
        modulusKey = null;
        key = null;
        xorKey = null;
        using var fileStream = File.OpenRead(fileName);
        var first = fileStream.ReadByte();
        var second = fileStream.ReadByte();
        if (first != 0x12 || second != 0x11)
        {
            return false;
        }

        var length = fileStream.ReadInteger();
        var remaining = fileStream.Length - fileStream.Position;
        // The previous check had the sign inverted (Length + Position), so a
        // truncated file was accepted. Require the declared payload to fit and to
        // be an exact multiple of the 3-key record size.
        if (length > remaining || length == 0 || length % (sizeof(uint) * 3) != 0)
        {
            return false;
        }

        var keyCount = (int)(length / (sizeof(uint) * 3));

        // The XOR key table only holds 16 entries; a larger file-driven count
        // would both index out of bounds and allow a huge allocation.
        const int maximumKeyCount = 16;
        if (keyCount > maximumKeyCount)
        {
            return false;
        }

        modulusKey = new uint[keyCount];
        key = new uint[keyCount];
        xorKey = new uint[keyCount];

        for (int i = 0; i < keyCount; i++)
        {
            modulusKey[i] = fileStream.ReadInteger() ^ this._encryptionKeys[i];
        }

        for (int i = 0; i < keyCount; i++)
        {
            key[i] = fileStream.ReadInteger() ^ this._encryptionKeys[i];
        }

        for (int i = 0; i < keyCount; i++)
        {
            xorKey[i] = fileStream.ReadInteger() ^ this._encryptionKeys[i];
        }

        return true;
    }
}