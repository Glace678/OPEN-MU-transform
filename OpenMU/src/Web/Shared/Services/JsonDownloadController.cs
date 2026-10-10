// <copyright file="JsonDownloadController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Services;

using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Json;

/// <summary>
/// Controller to retrieve data as json.
/// Supported types: <see cref="GenericControllerFeatureProvider.SupportedTypes"/>.
/// </summary>
/// <typeparam name="T">The source type of the data object.</typeparam>
/// <typeparam name="TSerializable">The type of the serializable.</typeparam>
[Route("download/[controller]")]
[GenericControllerName]

// Exporting persisted entities (notably any Account) is restricted to administrators: a plain
// [Authorize] let any authenticated low-privilege role (Viewer) pull arbitrary accounts by id.
[Authorize(Policy = "OpenMU.Administrator")]
public class JsonDownloadController<T, TSerializable> : ControllerBase
    where T : class
    where TSerializable : class
{
    private readonly IDataSource<T> _dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDownloadController{T,TSerializable}" /> class.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    public JsonDownloadController(IDataSource<T> dataSource)
    {
        this._dataSource = dataSource;
    }

    /// <summary>
    /// Gets the configuration with the specified id as json string.
    /// </summary>
    /// <param name="objectId">The identifier of the requested object.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The requested configuration as json string.
    /// </returns>
    [HttpGet("[controller]_{objectId}.json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async ValueTask<IActionResult> GetConfigurationByIdAsync(Guid objectId, CancellationToken cancellationToken)
    {
        // This is a GET (read-only) export: do not discard the data source's unsaved in-memory
        // changes here, that side effect could silently drop another admin's in-progress edits.
        var owner = await this._dataSource.GetOwnerAsync(objectId, cancellationToken).ConfigureAwait(false);

        if (owner is IConvertibleTo<TSerializable> convertibleTo)
        {
            var resultStream = new MemoryStream();
            await convertibleTo.Convert().ToJsonAsync(resultStream, cancellationToken).ConfigureAwait(false);
            resultStream.Position = 0;
            return this.File(resultStream, "application/json");
        }

        return this.NotFound();
    }
}