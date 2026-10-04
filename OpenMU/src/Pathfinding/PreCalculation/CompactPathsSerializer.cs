// <copyright file="CompactPathsSerializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Pathfinding.PreCalculation;

using System.IO;

/// <summary>
/// Serializes the path infos into a more compact format.
/// This format uses just 1 byte for end and 1 byte for next step.
/// It can only be used for maximumRange lower than 8, because of the space limitation.
/// </summary>
internal class CompactPathsSerializer : IPathsSerializer
{
    /// <inheritdoc/>
    public IEnumerable<PathInfo> Deserialize(Stream source)
    {
        const int elementSize = 4;
        const int CompactOffsetBias = 8;

        while (source.Position + elementSize <= source.Length)
        {
            byte startX = (byte)source.ReadByte();
            byte startY = (byte)source.ReadByte();
            byte startEndDiff = (byte)source.ReadByte();
            byte startNextStepDiff = (byte)source.ReadByte();
            var start = new Point(startX, startY);
            int xOffset = startEndDiff >> 4 & 0x0F;
            int yOffset = startEndDiff & 0x0F;
            var end = new Point((byte)(startX + xOffset - CompactOffsetBias), (byte)(startY + yOffset - CompactOffsetBias));

            int xOffsetNext = startNextStepDiff >> 4 & 0x0F;
            int yOffsetNext = startNextStepDiff & 0x0F;
            var nextStep = new Point((byte)(startX + xOffsetNext - CompactOffsetBias), (byte)(startY + yOffsetNext - CompactOffsetBias));
            yield return new PathInfo(new PointCombination(start, end), nextStep);
        }
    }

    /// <inheritdoc/>
    public void Serialize(IEnumerable<PathInfo> pathInfos, Stream targetStream)
    {
        foreach (var info in pathInfos)
        {
            targetStream.WriteByte(info.Combination.Start.X);
            targetStream.WriteByte(info.Combination.Start.Y);
            targetStream.WriteByte(CalcDiff(info.Combination.Start, info.Combination.End));
            targetStream.WriteByte(CalcDiff(info.Combination.Start, info.NextStep));
        }
    }

    private static byte CalcDiff(Point start, Point end)
    {
        int diffX = end.X - start.X + 8;
        int diffY = end.Y - start.Y + 8;
        if (diffX > 15)
        {
            throw new ArgumentException($"The difference between start and end in the x value is greater than the allowed 15. start: {start}, end: {end}");
        }

        if (diffY > 15)
        {
            throw new ArgumentException($"The difference between start and end in the y value is greater than the allowed 15. start: {start}, end: {end}");
        }

        return (byte)(((diffX << 4) & 0xF0) | (diffY & 0x0F));
    }
}