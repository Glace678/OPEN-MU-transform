// <copyright file="GameMapTerrainExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Extensions for the <see cref="GameMapTerrain"/>.
/// </summary>
public static class GameMapTerrainExtensions
{
    /// <summary>
    /// Renders the terrain into an image.
    /// </summary>
    /// <param name="terrain">The terrain.</param>
    /// <returns>The rendered image.</returns>
    public static RgbaImage ToImage(this GameMapTerrain terrain)
    {
        var bitmap = new RgbaImage(0x100, 0x100);
        for (int y = 0; y < 0x100; y++)
        {
            for (int x = 0; x < 0x100; x++)
            {
                if (terrain.SafezoneMap[y, x])
                {
                    bitmap.SetPixel(x, y, 128, 128, 128);
                }
                else if (terrain.WalkMap[y, x])
                {
                    bitmap.SetPixel(x, y, 80, 200, 80);
                }
                else
                {
                    // we use the default color.
                    bitmap.SetPixel(x, y, 0, 0, 0);
                }
            }
        }

        return bitmap;
    }
}
