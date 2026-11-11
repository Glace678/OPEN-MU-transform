// <copyright file="ReferenceResolvingConverterFactory.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Json;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Factory for <see cref="ReferenceResolvingConverter{T}"/>.
/// </summary>
public class ReferenceResolvingConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// A cache for previously created converters on this factory instance.
    /// </summary>
    private readonly ConcurrentDictionary<Type, JsonConverter> _convertersCache = new();

    /// <summary>
    /// Gets or sets the types which are ignored during deserialization.
    /// </summary>
    public Type[] IgnoredTypes { get; set; } = [];

    /// <inheritdoc />
    public override bool CanConvert(Type objectType) =>
        !objectType.IsEnum
        && !objectType.IsGenericType
        && objectType.IsAssignableTo(typeof(IIdentifiable))
        && (objectType.Namespace?.StartsWith("MUnique.OpenMU.", StringComparison.InvariantCulture) ?? false);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return this._convertersCache.GetOrAdd(typeToConvert, type =>
        {
            var converterType = typeof(ReferenceResolvingConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType, new object[] { this.IgnoredTypes })!;
        });
    }
}