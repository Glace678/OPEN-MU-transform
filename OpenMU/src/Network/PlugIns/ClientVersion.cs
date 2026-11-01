// <copyright file="ClientVersion.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.PlugIns;

/// <summary>
/// A definition about a client version.
/// </summary>
/// <param name="Season">The season.</param>
/// <param name="Episode">The episode.</param>
/// <param name="Language">The language.</param>
public record struct ClientVersion(byte Season, byte Episode, ClientLanguage Language) : IComparable<ClientVersion>, IComparable
{
    private int CombinedVersion => (this.Season << 8) + this.Episode;

    /// <summary>
    /// Implements the operator &gt;.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator >(ClientVersion left, ClientVersion right) => Compare(left, right) > 0;

    /// <summary>
    /// Implements the operator &gt;=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator >=(ClientVersion left, ClientVersion right) => Compare(left, right) >= 0;

    /// <summary>
    /// Implements the operator &lt;.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator <(ClientVersion left, ClientVersion right) => Compare(left, right) < 0;

    /// <summary>
    /// Implements the operator &lt;=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator <=(ClientVersion left, ClientVersion right) => Compare(left, right) <= 0;

    /// <inheritdoc />
    public int CompareTo(ClientVersion other)
    {
        var result = (this.CombinedVersion - other.CombinedVersion) * 10;
        if (this.Language != other.Language)
        {
            if (this.Language != ClientLanguage.Invariant && other.Language == ClientLanguage.Invariant)
            {
                result++;
            }
            else if (this.Language == ClientLanguage.Invariant && other.Language != ClientLanguage.Invariant)
            {
                result--;
            }
            else
            {
                result += Math.Sign(this.Language.CompareTo(other.Language));
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is ClientVersion other)
        {
            return this.CompareTo(other);
        }

        throw new ArgumentException($"Object must be of type {nameof(ClientVersion)}", nameof(obj));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (this.Season > 0)
        {
            return $"Season {this.Season} Episode {this.Episode} {this.Language}";
        }

        return $"{this.Season}.{this.Episode} {this.Language}";
    }

    private static int Compare(ClientVersion left, ClientVersion right) => left.CompareTo(right);
}