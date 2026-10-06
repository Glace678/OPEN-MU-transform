// <copyright file="SetupServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Tests the three-state data initialization probe of <see cref="SetupService"/>
/// (XC-13/XC-20): a query failure must surface as Unknown and be distinguishable
/// from a reachable-but-empty database.
/// </summary>
[TestFixture]
public class SetupServiceTests
{
    /// <summary>A reachable database without a configuration is reported Empty.</summary>
    [Test]
    public async Task EmptyDatabaseIsReportedAsEmptyAsync()
    {
        var service = CreateService((Guid?)null);

        Assert.That(await service.GetDataInitializationStateAsync().ConfigureAwait(false),
            Is.EqualTo(SetupService.DataInitializationState.Empty));
    }

    /// <summary>A database returning a configuration id is reported Initialized.</summary>
    [Test]
    public async Task InitializedDatabaseIsReportedAsInitializedAsync()
    {
        var service = CreateService(Guid.NewGuid());

        Assert.That(await service.GetDataInitializationStateAsync().ConfigureAwait(false),
            Is.EqualTo(SetupService.DataInitializationState.Initialized));
    }

    /// <summary>A query exception is reported as Unknown rather than Empty.</summary>
    [Test]
    public async Task QueryFailureIsReportedAsUnknownAsync()
    {
        var context = new Mock<IConfigurationContext>();
        context.Setup(c => c.GetDefaultGameConfigurationIdAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("database is down"));
        var service = CreateService(context.Object);

        Assert.That(await service.GetDataInitializationStateAsync().ConfigureAwait(false),
            Is.EqualTo(SetupService.DataInitializationState.Unknown));
    }

    /// <summary>A hanging database query is cancelled after the timeout and reported Unknown.</summary>
    [Test]
    public async Task HangingQueryTimesOutAndIsUnknownAsync()
    {
        var context = new Mock<IConfigurationContext>();
        context.Setup(c => c.GetDefaultGameConfigurationIdAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return (Guid?)null;
            });
        var service = CreateService(context.Object);

        Assert.That(await service.GetDataInitializationStateAsync().ConfigureAwait(false),
            Is.EqualTo(SetupService.DataInitializationState.Unknown));
    }

    private static SetupService CreateService(Guid? configurationId)
    {
        var context = new Mock<IConfigurationContext>();
        context.Setup(c => c.GetDefaultGameConfigurationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configurationId);
        return CreateService(context.Object);
    }

    private static SetupService CreateService(IConfigurationContext context)
    {
        var provider = new Mock<IMigratableDatabaseContextProvider>();
        provider.Setup(p => p.CreateNewConfigurationContext()).Returns(context);
        return new SetupService(provider.Object, new PlugInManager(null, NullLoggerFactory.Instance, null, null));
    }
}
