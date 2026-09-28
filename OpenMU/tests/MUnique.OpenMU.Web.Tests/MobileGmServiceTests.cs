// <copyright file="MobileGmServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Web.AdminPanel.API;

/// <summary>
/// Tests the context-independent mobile GM request boundary and idempotency behavior.
/// </summary>
[TestFixture]
public class MobileGmServiceTests
{
    /// <summary>Only bits for available option numbers may be requested, including sparse definitions.</summary>
    /// <param name="mask">The requested option bits.</param>
    /// <param name="expected">Whether the request is valid.</param>
    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(8, true)]
    [TestCase(10, true)]
    [TestCase(1, false)]
    [TestCase(4, false)]
    [TestCase(11, false)]
    [TestCase(16, false)]
    [TestCase(-1, false)]
    public void ExcellentOptionsMustExistInTheItemDefinition(int mask, bool expected)
    {
        var options = new[]
        {
            new IncreasableItemOption { Number = 2 },
            new IncreasableItemOption { Number = 4 },
        };

        Assert.That(MobileGmService.HasValidExcellentOptions(mask, options), Is.EqualTo(expected));
    }

    /// <summary>Invalid definition numbers cannot wrap around and enable an unrelated bit.</summary>
    [Test]
    public void InvalidExcellentOptionNumbersCannotEnableOtherBits()
    {
        var options = new[]
        {
            new IncreasableItemOption { Number = 0 },
            new IncreasableItemOption { Number = 32 },
            new IncreasableItemOption { Number = 33 },
        };

        Assert.That(MobileGmService.HasValidExcellentOptions(1, options), Is.False);
    }

    /// <summary>Valid requests accept the documented limits.</summary>
    [TestCase(1, 0)]
    [TestCase(10, 255)]
    public void ValidRequestIsAccepted(int quantity, int level)
    {
        var request = CreateRequest(quantity: quantity, level: level);

        Assert.That(MobileGmService.ValidateRequest(request, out var requestId, out var characterId), Is.Null);
        Assert.That(requestId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(characterId, Is.Not.EqualTo(Guid.Empty));
    }

    /// <summary>Malformed identifiers and out-of-range fields are rejected before touching a player.</summary>
    [TestCase("bad", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0, 0, 0, 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "bad", 0, 0, 0, 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", -1, 0, 0, 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0, -1, 0, 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0, 0, -1, 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0, 0, 0, 0)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0, 0, 0, 11)]
    public void InvalidRequestIsRejected(
        string requestId,
        string characterId,
        int group,
        int number,
        int level,
        int quantity)
    {
        var request = new MobileGmGrantRequest(requestId, characterId, group, number, level, quantity, false);

        Assert.That(MobileGmService.ValidateRequest(request, out _, out _), Is.Not.Null.And.Not.Empty);
    }

    /// <summary>Valid Zen requests accept the documented limits.</summary>
    [TestCase(1)]
    [TestCase(2_000_000_000)]
    public void ValidZenRequestIsAccepted(long amount)
    {
        var request = CreateZenRequest(amount: amount);

        Assert.That(MobileGmService.ValidateZenRequest(request, out var requestId, out var characterId, out var validatedAmount), Is.Null);
        Assert.That(requestId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(characterId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(validatedAmount, Is.EqualTo(amount));
    }

    /// <summary>Malformed identifiers and out-of-range Zen amounts are rejected before touching a player.</summary>
    [TestCase("bad", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "bad", 1)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 0)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", -5)]
    [TestCase("0dd5ac9b-8288-4ec1-a129-9b788d0a8a12", "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8", 2_000_000_001L)]
    public void InvalidZenRequestIsRejected(string requestId, string characterId, long amount)
    {
        var request = new MobileGmZenRequest(requestId, characterId, amount);

        Assert.That(MobileGmService.ValidateZenRequest(request, out _, out _, out _), Is.Not.Null.And.Not.Empty);
    }

    /// <summary>Out-of-range additional option level is rejected.</summary>
    [Test]
    public void InvalidAdditionalOptionLevelIsRejected()
    {
        var request = CreateRequest() with { AdditionalOptionLevel = 5 };

        Assert.That(MobileGmService.ValidateRequest(request, out _, out _), Is.Not.Null.And.Not.Empty);
    }

    /// <summary>Negative excellent mask is rejected.</summary>
    [Test]
    public void NegativeExcellentMaskIsRejected()
    {
        var request = CreateRequest() with { ExcellentMask = -1 };

        Assert.That(MobileGmService.ValidateRequest(request, out _, out _), Is.Not.Null.And.Not.Empty);
    }

    /// <summary>Repeated identical Zen IDs share one operation and conflicting payloads are rejected.</summary>
    [Test]
    public async Task ZenRequestIsIdempotentAsync()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var service = new MobileGmService(services, NullLogger<MobileGmService>.Instance);
        var request = CreateZenRequest();

        var first = service.GrantZenAsync(request);
        var duplicate = service.GrantZenAsync(request);
        var conflict = await service.GrantZenAsync(request with { Amount = 500 }).ConfigureAwait(false);

        Assert.That(duplicate, Is.SameAs(first));
        Assert.That(conflict.Success, Is.False);
        Assert.That(conflict.Message, Does.Contain("requestId"));
    }

    /// <summary>Repeated identical IDs share one operation and conflicting payloads are rejected.</summary>
    [Test]
    public async Task GrantRequestIsIdempotentAsync()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var service = new MobileGmService(services, NullLogger<MobileGmService>.Instance);
        var request = CreateRequest();

        var first = service.GrantItemAsync(request);
        var duplicate = service.GrantItemAsync(request);
        var conflict = await service.GrantItemAsync(request with { Quantity = 2 }).ConfigureAwait(false);

        Assert.That(duplicate, Is.SameAs(first));
        Assert.That(conflict.Success, Is.False);
        Assert.That(conflict.Message, Does.Contain("requestId"));
    }

    private static MobileGmGrantRequest CreateRequest(int quantity = 1, int level = 0) =>
        new(
            "0dd5ac9b-8288-4ec1-a129-9b788d0a8a12",
            "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8",
            0,
            0,
            level,
            quantity,
            false);

    private static MobileGmZenRequest CreateZenRequest(long amount = 1_000_000) =>
        new(
            "71c1f3a2-9b4d-4e7a-8c2f-5d6e7f8091ab",
            "3eb0ac95-fdd2-4b55-bd45-9796b4fc9ba8",
            amount);
}
