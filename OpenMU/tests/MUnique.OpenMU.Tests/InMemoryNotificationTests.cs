// <copyright file="InMemoryNotificationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>Tests configuration notification suspension during in-memory initialization.</summary>
[TestFixture]
public sealed class InMemoryNotificationTests
{
    /// <summary>Verifies a suspended save is silent and later saves notify again.</summary>
    [Test]
    public async Task SuspendedSavesDoNotPublishAndResumeAfterDisposalAsync()
    {
        using var context = new InMemoryContext(new InMemoryRepositoryProvider());
        var notifications = 0;
        context.SavedChanges += (_, _) => notifications++;

        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(notifications, Is.EqualTo(1));
        using (context.SuspendChangeNotifications())
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
            Assert.That(notifications, Is.EqualTo(1));
        }

        Assert.That(notifications, Is.EqualTo(1), "Resuming must not replay initialization changes.");
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(notifications, Is.EqualTo(2));
    }

    /// <summary>Verifies nested scopes and repeated disposal do not resume prematurely.</summary>
    [Test]
    public async Task NestedSuspensionsRemainSilentUntilEveryScopeIsDisposedAsync()
    {
        using var context = new InMemoryContext(new InMemoryRepositoryProvider());
        var notifications = 0;
        context.SavedChanges += (_, _) => notifications++;
        using var outer = context.SuspendChangeNotifications();
        using var inner = context.SuspendChangeNotifications();

        outer.Dispose();
        outer.Dispose();
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(notifications, Is.Zero);

        inner.Dispose();
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(notifications, Is.EqualTo(1));
    }

    /// <summary>Verifies the provider cannot resolve plug-in services during a suspended initialization save.</summary>
    [Test]
    public async Task InitializationSaveDoesNotReachConfigurationPublisherAsync()
    {
        var publisher = new Mock<IConfigurationChangePublisher>(MockBehavior.Strict);
        publisher.Setup(instance => instance.ConfigurationChangedAsync(
            typeof(PlugInConfiguration), It.IsAny<Guid>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        var provider = new InMemoryPersistenceContextProvider(publisher.Object);
        using var context = provider.CreateNewContext();
        _ = context.CreateNew<PlugInConfiguration>();

        using (context.SuspendChangeNotifications())
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
            publisher.Verify(instance => instance.ConfigurationChangedAsync(
                It.IsAny<Type>(), It.IsAny<Guid>(), It.IsAny<object>()), Times.Never);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        publisher.Verify(instance => instance.ConfigurationChangedAsync(
            typeof(PlugInConfiguration), It.IsAny<Guid>(), It.IsAny<object>()), Times.Once);
    }
}
