// <copyright file="RgbaImage.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared;

using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

/// <summary>
/// A minimal, dependency-free 32-bit RGBA raster image which can be encoded as PNG.
/// </summary>
/// <remarks>
/// The web projects only need to render small terrain maps and embed them as PNG.
/// This type replaces the former SixLabors.ImageSharp dependency, whose newer
/// (security-patched) releases are no longer OSI-licensed and require a license
/// key for release builds.
/// </remarks>
public sealed class RgbaImage
{
    private const int BytesPerPixel = 4;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = CreateCrcTable();

    private readonly byte[] _pixels;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaImage"/> class.
    /// All pixels are initialized to transparent black.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public RgbaImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        this.Width = width;
        this.Height = height;
        this._pixels = new byte[checked(width * height * BytesPerPixel)];
    }

    /// <summary>
    /// Gets the width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Sets the color of a pixel.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <param name="alpha">The alpha component.</param>
    public void SetPixel(int x, int y, byte red, byte green, byte blue, byte alpha = byte.MaxValue)
    {
        var offset = this.GetOffset(x, y);
        this._pixels[offset] = red;
        this._pixels[offset + 1] = green;
        this._pixels[offset + 2] = blue;
        this._pixels[offset + 3] = alpha;
    }

    /// <summary>
    /// Gets the color of a pixel.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The red, green, blue and alpha components of the pixel.</returns>
    public (byte Red, byte Green, byte Blue, byte Alpha) GetPixel(int x, int y)
    {
        var offset = this.GetOffset(x, y);
        return (this._pixels[offset], this._pixels[offset + 1], this._pixels[offset + 2], this._pixels[offset + 3]);
    }

    /// <summary>
    /// Encodes the image as PNG (8 bit per channel, RGBA, non-interlaced).
    /// </summary>
    /// <returns>The PNG file content.</returns>
    public byte[] ToPng()
    {
        using var output = new MemoryStream();
        output.Write(PngSignature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, this.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], this.Height);
        header[8] = 8; // bit depth
        header[9] = 6; // color type: truecolor with alpha
        header[10] = 0; // compression method: deflate
        header[11] = 0; // filter method: adaptive
        header[12] = 0; // interlace method: none
        WriteChunk(output, "IHDR"u8, header);

        WriteChunk(output, "IDAT"u8, this.CreateCompressedScanlines());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    /// <summary>
    /// Encodes the image as PNG and returns it as data URL, which can be embedded directly into an html img-element.
    /// </summary>
    /// <returns>The data URL of the PNG encoded image.</returns>
    public string ToPngDataUrl()
    {
        return "data:image/png;base64," + Convert.ToBase64String(this.ToPng());
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);
        output.Write(type);
        output.Write(data);

        var crc = UpdateCrc(0xFFFFFFFFu, type);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc ^ 0xFFFFFFFFu);
        output.Write(buffer);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private byte[] CreateCompressedScanlines()
    {
        var stride = this.Width * BytesPerPixel;
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < this.Height; y++)
            {
                zlib.WriteByte(0); // filter type: none
                zlib.Write(this._pixels, y * stride, stride);
            }
        }

        return compressed.ToArray();
    }

    private int GetOffset(int x, int y)
    {
        if ((uint)x >= (uint)this.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)this.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return ((y * this.Width) + x) * BytesPerPixel;
    }
}
