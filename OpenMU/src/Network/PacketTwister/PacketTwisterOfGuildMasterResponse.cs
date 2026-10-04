// <copyright file="PacketTwisterOfGuildMasterResponse.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.PacketTwister;

/// <summary>
/// PacketTwister implementation for packets of 'GuildMasterResponse' type.
/// </summary>
/// <remarks>Correct is the true inverse of Twist (reverse order, right-rotate instead of left-rotate).</remarks>
internal class PacketTwisterOfGuildMasterResponse : IPacketTwister
{
    /// <inheritdoc/>
    public void Twist(Span<byte> data)
    {
        if (data.Length >= 4)
        {
            if (data.Length >= 8)
            {
                if (data.Length >= 16)
                {
                    if (data.Length >= 32)
                    {
                        var v11 = (byte)(data[17] >> 6);
                        data[17] *= 4;
                        data[17] |= v11;
                        var v12 = data[6];
                        data[6] = data[4];
                        data[4] = v12;
                        var v18 = (byte)((data[21] >> 2) & 1);
                        if (((data[21] >> 4) & 1) != 0)
                        {
                            data[21] |= 4;
                        }
                        else
                        {
                            data[21] &= 0xFB;
                        }

                        if (v18 != 0)
                        {
                            data[21] |= 0x10;
                        }
                        else
                        {
                            data[21] &= 0xEF;
                        }

                        var v13 = data[22];
                        data[22] = data[17];
                        data[17] = v13;
                        data[23] ^= 0xB4;
                        var v17 = (byte)((data[19] >> 7) & 1);
                        if (((data[19] >> 7) & 1) != 0)
                        {
                            data[19] |= 0x80;
                        }
                        else
                        {
                            data[19] &= 0x7F;
                        }

                        if (v17 != 0)
                        {
                            data[19] |= 0x80;
                        }
                        else
                        {
                            data[19] &= 0x7F;
                        }

                        var v16 = (byte)((data[17] >> 7) & 1);
                        if (((data[17] >> 5) & 1) != 0)
                        {
                            data[17] |= 0x80;
                        }
                        else
                        {
                            data[17] &= 0x7F;
                        }

                        if (v16 != 0)
                        {
                            data[17] |= 0x20;
                        }
                        else
                        {
                            data[17] &= 0xDF;
                        }

                        data[5] ^= 0x40;
                        var v14 = data[23];
                        data[23] = data[12];
                        data[12] = v14;
                        var v15 = (byte)((data[27] >> 1) & 1);
                        if (((data[27] >> 2) & 1) != 0)
                        {
                            data[27] |= 2;
                        }
                        else
                        {
                            data[27] &= 0xFD;
                        }

                        if (v15 != 0)
                        {
                            data[27] |= 4;
                        }
                        else
                        {
                            data[27] &= 0xFB;
                        }
                    }
                    else
                    {
                        data[3] ^= 0x2F;
                        data[4] ^= 0x17;
                        data[15] ^= 0x5B;
                        data[7] ^= 0x53;
                        var v19 = (byte)((data[11] >> 7) & 1);
                        if (((data[11] >> 3) & 1) != 0)
                        {
                            data[11] |= 0x80;
                        }
                        else
                        {
                            data[11] &= 0x7F;
                        }

                        if (v19 != 0)
                        {
                            data[11] |= 8;
                        }
                        else
                        {
                            data[11] &= 0xF7;
                        }

                        data[3] ^= 0x89;
                        data[14] ^= 0x6D;
                        var v9 = data[13];
                        data[13] = data[9];
                        data[9] = v9;
                        data[12] ^= 0xFC;
                        data[4] ^= 0xF7;
                        var v10 = data[5];
                        data[5] = data[4];
                        data[4] = v10;
                    }
                }
                else
                {
                    var v7 = (byte)(data[5] >> 6);
                    data[5] *= 4;
                    data[5] |= v7;
                    var v8 = (byte)(data[7] >> 7);
                    data[7] *= 2;
                    data[7] |= v8;
                    data[1] ^= 0x98;
                    var v20 = (byte)((data[6] >> 2) & 1);
                    if (((data[6] >> 7) & 1) != 0)
                    {
                        data[6] |= 4;
                    }
                    else
                    {
                        data[6] &= 0xFB;
                    }

                    if (v20 != 0)
                    {
                        data[6] |= 0x80;
                    }
                    else
                    {
                        data[6] &= 0x7F;
                    }

                    data[3] ^= 0xDD;
                    data[1] ^= 6;
                    data[4] ^= 0x7E;
                }
            }
            else
            {
                data[2] ^= 0xCF;
                data[0] ^= 0xD6;
                var v3 = data[3];
                data[3] = data[2];
                data[2] = v3;
                data[2] ^= 0x72;
                data[0] ^= 0xE2;
                var v4 = data[2];
                data[2] = data[3];
                data[3] = v4;
                var v5 = data[0];
                data[0] = data[3];
                data[3] = v5;
                var v6 = (byte)(data[3] >> 1);
                data[3] <<= 7;
                data[3] |= v6;
                var v2 = (byte)((data[1] >> 2) & 1);
                if (((data[1] >> 5) & 1) != 0)
                {
                    data[1] |= 4;
                }
                else
                {
                    data[1] &= 0xFB;
                }

                if (v2 != 0)
                {
                    data[1] |= 0x20;
                }
                else
                {
                    data[1] &= 0xDF;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Correct(Span<byte> data)
    {
        // This is the true inverse of <see cref="Twist"/>: the operations of Twist are
        // applied in reverse order. XOR, swaps and pairwise bit swaps are self-inverse,
        // so they are repeated unchanged; every left-rotate (rol) is replaced by its
        // inverse right-rotate (ror). This guarantees Correct(Twist(data)) == data.
        if (data.Length >= 4)
        {
            if (data.Length >= 8)
            {
                if (data.Length >= 16)
                {
                    if (data.Length >= 32)
                    {
                        // data[27]: swap bits 1 <-> 2.
                        var v15 = (byte)((data[27] >> 1) & 1);
                        if (((data[27] >> 2) & 1) != 0)
                        {
                            data[27] |= 2;
                        }
                        else
                        {
                            data[27] &= 0xFD;
                        }

                        if (v15 != 0)
                        {
                            data[27] |= 4;
                        }
                        else
                        {
                            data[27] &= 0xFB;
                        }

                        // swap(data[23], data[12])
                        var v14 = data[23];
                        data[23] = data[12];
                        data[12] = v14;

                        data[5] ^= 0x40;

                        // data[17]: swap bits 5 <-> 7.
                        var v16 = (byte)((data[17] >> 7) & 1);
                        if (((data[17] >> 5) & 1) != 0)
                        {
                            data[17] |= 0x80;
                        }
                        else
                        {
                            data[17] &= 0x7F;
                        }

                        if (v16 != 0)
                        {
                            data[17] |= 0x20;
                        }
                        else
                        {
                            data[17] &= 0xDF;
                        }

                        data[23] ^= 0xB4;

                        // swap(data[22], data[17])
                        var v13 = data[22];
                        data[22] = data[17];
                        data[17] = v13;

                        // data[21]: swap bits 2 <-> 4.
                        var v18 = (byte)((data[21] >> 2) & 1);
                        if (((data[21] >> 4) & 1) != 0)
                        {
                            data[21] |= 4;
                        }
                        else
                        {
                            data[21] &= 0xFB;
                        }

                        if (v18 != 0)
                        {
                            data[21] |= 0x10;
                        }
                        else
                        {
                            data[21] &= 0xEF;
                        }

                        // swap(data[6], data[4])
                        var v12 = data[6];
                        data[6] = data[4];
                        data[4] = v12;

                        // ror2(data[17]) - inverse of rol2.
                        var r11 = (byte)(data[17] << 6);
                        data[17] = (byte)(data[17] >> 2);
                        data[17] |= r11;
                    }
                    else
                    {
                        // [16,32) branch: all involutions, reverse order.
                        // swap(data[5], data[4])
                        var v10 = data[5];
                        data[5] = data[4];
                        data[4] = v10;

                        data[4] ^= 0xF7;
                        data[12] ^= 0xFC;

                        // swap(data[13], data[9])
                        var v9 = data[13];
                        data[13] = data[9];
                        data[9] = v9;

                        data[14] ^= 0x6D;
                        data[3] ^= 0x89;

                        // data[11]: swap bits 7 <-> 3.
                        var v19 = (byte)((data[11] >> 7) & 1);
                        if (((data[11] >> 3) & 1) != 0)
                        {
                            data[11] |= 0x80;
                        }
                        else
                        {
                            data[11] &= 0x7F;
                        }

                        if (v19 != 0)
                        {
                            data[11] |= 8;
                        }
                        else
                        {
                            data[11] &= 0xF7;
                        }

                        data[7] ^= 0x53;
                        data[15] ^= 0x5B;
                        data[4] ^= 0x17;
                        data[3] ^= 0x2F;
                    }
                }
                else
                {
                    // [8,16) branch.
                    data[4] ^= 0x7E;
                    data[1] ^= 6;
                    data[3] ^= 0xDD;

                    // data[6]: swap bits 2 <-> 7.
                    var v20 = (byte)((data[6] >> 2) & 1);
                    if (((data[6] >> 7) & 1) != 0)
                    {
                        data[6] |= 4;
                    }
                    else
                    {
                        data[6] &= 0xFB;
                    }

                    if (v20 != 0)
                    {
                        data[6] |= 0x80;
                    }
                    else
                    {
                        data[6] &= 0x7F;
                    }

                    data[1] ^= 0x98;

                    // ror1(data[7]) - inverse of rol1.
                    var r8 = (byte)(data[7] << 7);
                    data[7] = (byte)(data[7] >> 1);
                    data[7] |= r8;

                    // ror2(data[5]) - inverse of rol2.
                    var r7 = (byte)(data[5] << 6);
                    data[5] = (byte)(data[5] >> 2);
                    data[5] |= r7;
                }
            }
            else
            {
                // [4,8) branch.
                // data[1]: swap bits 2 <-> 5.
                var v2 = (byte)((data[1] >> 2) & 1);
                if (((data[1] >> 5) & 1) != 0)
                {
                    data[1] |= 4;
                }
                else
                {
                    data[1] &= 0xFB;
                }

                if (v2 != 0)
                {
                    data[1] |= 0x20;
                }
                else
                {
                    data[1] &= 0xDF;
                }

                // ror7(data[3]) - inverse of rol7.
                var r6 = (byte)(data[3] << 1);
                data[3] = (byte)(data[3] >> 7);
                data[3] |= r6;

                // swap(data[0], data[3])
                var v5 = data[0];
                data[0] = data[3];
                data[3] = v5;

                // swap(data[2], data[3])
                var v4 = data[2];
                data[2] = data[3];
                data[3] = v4;

                data[0] ^= 0xE2;
                data[2] ^= 0x72;

                // swap(data[3], data[2])
                var v3 = data[3];
                data[3] = data[2];
                data[2] = v3;

                data[0] ^= 0xD6;
                data[2] ^= 0xCF;
            }
        }
    }
}
