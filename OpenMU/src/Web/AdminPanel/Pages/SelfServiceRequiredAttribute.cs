// <copyright file="SelfServiceRequiredAttribute.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Required-value validation which resolves its message from the current UI culture.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
internal sealed class SelfServiceRequiredAttribute : RequiredAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelfServiceRequiredAttribute"/> class.
    /// </summary>
    /// <param name="resourceKey">The resource key used for the validation message.</param>
    public SelfServiceRequiredAttribute(string resourceKey)
    {
        this.ResourceKey = resourceKey;
    }

    /// <summary>
    /// Gets the resource key used for the validation message.
    /// </summary>
    private string ResourceKey { get; }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var hasValue = value switch
        {
            null => false,
            string text => this.AllowEmptyStrings || !string.IsNullOrWhiteSpace(text),
            _ => true,
        };
        if (hasValue)
        {
            return ValidationResult.Success;
        }

        var message = SelfServiceResourceLookup.Get(this.ResourceKey);
        return new ValidationResult(message, validationContext.MemberName is { } memberName ? [memberName] : null);
    }
}
