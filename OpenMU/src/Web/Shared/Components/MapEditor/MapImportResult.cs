// <copyright file="MapImportResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.MapEditor;

/// <summary>
/// The outcome of a map spawn import.
/// </summary>
/// <remarks>
/// A failure never modifies the target map: the import validates the whole payload before
/// deleting any existing spawn, so a rejected import leaves the current map untouched and
/// can be retried with a corrected file.
/// </remarks>
public sealed record MapImportResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapImportResult"/> class.
    /// </summary>
    /// <param name="success">If set to <c>true</c>, the import was applied.</param>
    /// <param name="importedCount">The number of spawn areas imported on success.</param>
    /// <param name="errorMessage">The error message when the import was rejected.</param>
    private MapImportResult(bool success, int importedCount, string? errorMessage)
    {
        this.Success = success;
        this.ImportedCount = importedCount;
        this.ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets a value indicating whether the import was applied.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the number of spawn areas imported.
    /// </summary>
    public int ImportedCount { get; }

    /// <summary>
    /// Gets the error message when the import was rejected; otherwise, <c>null</c>.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="count">The number of imported spawn areas.</param>
    /// <returns>The result.</returns>
    public static MapImportResult SuccessResult(int count) => new(true, count, null);

    /// <summary>
    /// Creates a failure result; the target map was not modified.
    /// </summary>
    /// <param name="message">The rejection reason.</param>
    /// <returns>The result.</returns>
    public static MapImportResult Failure(string message) => new(false, 0, message);
}