// <copyright file="AccountTextEndpointTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>Public player text has explicit language selection and cannot access account storage.</summary>
[TestFixture]
public class AccountTextEndpointTests
{
    /// <summary>Each portal language is served without needing a Blazor circuit or authentication cookie.</summary>
    [TestCase("en")]
    [TestCase("zh-CN")]
    [TestCase("zh-TW")]
    [TestCase("ja")]
    [TestCase("ko")]
    [TestCase("de")]
    [TestCase("es")]
    [TestCase("fr")]
    [TestCase("pt")]
    [TestCase("ru")]
    [TestCase("uk")]
    [TestCase("pl")]
    [TestCase("id")]
    [TestCase("vi")]
    [TestCase("tl")]
    [TestCase("unsupported", "en")]
    public async Task PublicTextIsLocalizedAndDoesNotReadStorageAsync(string culture, string? expectedCulture = null)
    {
        var persistence = new Mock<IPersistenceContextProvider>(MockBehavior.Strict);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(persistence.Object);
        builder.Services.AddLocalization();
        await using var app = builder.Build();
        app.MapPublicRegistrationEndpoints();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == "/api/registration/text");
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString("?culture=" + culture);
        context.Response.Body = body;

        await endpoint.RequestDelegate!(context).ConfigureAwait(false);
        body.Position = 0;
        var strings = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(body).ConfigureAwait(false);
        var manager = new ResourceManager("MUnique.OpenMU.Web.AdminPanel.Properties.SelfServiceResources", typeof(SelfServiceResources).Assembly);
        Assert.That(strings!["RecoveryCodeLabel"], Is.EqualTo(manager.GetString("RecoveryCodeLabel", CultureInfo.GetCultureInfo(expectedCulture ?? culture))));
        Assert.That(strings, Does.Not.ContainKey("RegisterAdminLink"));
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        persistence.VerifyNoOtherCalls();
    }
}
