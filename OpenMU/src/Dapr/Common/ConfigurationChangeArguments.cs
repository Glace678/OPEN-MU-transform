// <copyright file="ConfigurationChangeArguments.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

/// <summary>
/// Arguments for the change notifications of <see cref="ConfigurationChangePublisher"/>.
/// </summary>
public record class ConfigurationChangeArguments(string TypeName, Guid Id, object? Configuration)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationChangeArguments"/> class.
    /// </summary>
    /// <param name="type">The type of the configuration.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="configuration">The configuration.</param>
    public ConfigurationChangeArguments(Type type, Guid id, object? configuration)
        : this(type.FullName ?? throw new ArgumentException("The type has no full name.", nameof(type)), id, configuration)
    {
    }

    /// <summary>
    /// Resolves the configuration type from the serialized <see cref="TypeName"/>.
    /// </summary>
    /// <returns>The resolved <see cref="Type"/>.</returns>
    /// <exception cref="InvalidOperationException">The type could not be resolved.</exception>
    public Type ResolveType()
        => AppDomain.CurrentDomain.GetAssemblies()
               .Select(a => a.GetType(this.TypeName, throwOnError: false))
               .FirstOrDefault(t => t is not null)
           ?? throw new InvalidOperationException($"Could not resolve configuration type '{this.TypeName}'.");
}