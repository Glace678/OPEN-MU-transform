// <copyright file="MapInterfaceAuthorizationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence.BasicModel;
using MUnique.OpenMU.Web.Map.Map;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>
/// Authorization-metadata tests for the map interface (80-01) and the JSON entity download
/// endpoint (80-75). These assert the server-side authorization requirement declared on the
/// controllers; the host pipeline (Dapr internal-only middleware / AdminPanel policy) enforces
/// 401/403 for anonymous or insufficient-privilege callers.
/// </summary>
[TestFixture]
public class MapInterfaceAuthorizationTests
{
    [Test]
    public void TerrainController_RequiresAuthorization()
    {
        var authorize = typeof(TerrainController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>().ToList();
        Assert.That(authorize, Is.Not.Empty, "TerrainController must be decorated with [Authorize].");

        var anonymous = typeof(TerrainController).GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);
        Assert.That(anonymous, Is.Empty, "TerrainController must not opt out with [AllowAnonymous].");
    }

    [Test]
    public void JsonDownloadController_RequiresAdministratorPolicy()
    {
        // The generic controller is closed for Account by GenericControllerFeatureProvider.
        var closed = typeof(JsonDownloadController<,>).MakeGenericType(typeof(MUnique.OpenMU.DataModel.Entities.Account), typeof(MUnique.OpenMU.Persistence.BasicModel.Account));

        var authorize = closed.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>().ToList();
        Assert.That(authorize, Is.Not.Empty, "JsonDownloadController must require authorization.");
        Assert.That(authorize.Select(a => a.Policy), Has.Some.EqualTo("OpenMU.Administrator"),
            "JsonDownloadController must be restricted to the Administrator policy so a low-privilege "
            + "authenticated role cannot export arbitrary accounts by id.");
    }
}