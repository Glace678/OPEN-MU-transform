// <copyright file="CapturedConnectionExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Analyzer;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

/// <summary>
/// Extensions for <see cref="ICapturedConnection"/>.
/// </summary>
public static class CapturedConnectionExtensions
{
    private static char _fieldSeparator = ';';

    /// <summary>
    /// Saves the captured packets to the file in a CSV format.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="path">The path.</param>
    public static void SaveToFile(this ICapturedConnection connection, string path)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file);
        writer.WriteLine(connection.StartTimestamp);
        foreach (var packet in connection.PacketList)
        {
            writer.Write(packet.Timestamp.Ticks);
            writer.Write(_fieldSeparator);
            writer.Write(packet.ToServer);
            writer.Write(_fieldSeparator);
            writer.Write(packet.Size);
            writer.Write(_fieldSeparator);
            writer.WriteLine(packet.PacketData);
        }
    }

    /// <summary>
    /// Loads the packets from the file.
    /// </summary>
    /// <param name="packetList">The packet list.</param>
    /// <param name="path">The path.</param>
    /// <returns>The <see cref="ICapturedConnection.StartTimestamp"/>.</returns>
    public static DateTime LoadFromFile(this BindingList<Packet> packetList, string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        DateTime.TryParse(reader.ReadLine(), out var start);

        while (!reader.EndOfStream)
        {
            var currentLine = reader.ReadLine();
            var splittedLine = currentLine?.Split(_fieldSeparator);
            if (splittedLine is { Length: > 3 }
                && long.TryParse(splittedLine[0], out var ticks)
                && bool.TryParse(splittedLine[1], out var toServer)
                && int.TryParse(splittedLine[2], out var size)
                && TryParseArray(splittedLine[3], size, out var data))
            {
                packetList.Add(new Packet(new TimeSpan(ticks), data, toServer));
            }
            else
            {
                Debug.Fail($"Invalid line: {currentLine}");
            }
        }

        return start;
    }

    /// <summary>
    /// Tries to parse the byte array string.
    /// </summary>
    /// <param name="arrayString">The array string.</param>
    /// <param name="data">The resulting byte array.</param>
    /// <returns>The success of the parsing.</returns>
    public static bool TryParseArray(string arrayString, out byte[] data)
    {
        return TryParseArray(arrayString, 0, out data);
    }

    private const int MaximumImportPacketSize = 65535;

    private static bool TryParseArray(string arrayString, int specifiedLength, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (specifiedLength is < 0 or > MaximumImportPacketSize)
        {
            return false;
        }

        var bytesAsString = arrayString.Split(' ');
        if (specifiedLength != 0 && bytesAsString.Length != specifiedLength)
        {
            return false;
        }

        var arrayLength = bytesAsString.Length;
        if (arrayLength > MaximumImportPacketSize)
        {
            return false;
        }

        data = new byte[arrayLength];

        for (int i = 0; i < bytesAsString.Length; i++)
        {
            if (!byte.TryParse(bytesAsString[i], System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out data[i]))
            {
                return false;
            }
        }

        return true;
    }
}