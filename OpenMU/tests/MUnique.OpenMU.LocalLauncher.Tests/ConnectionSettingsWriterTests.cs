// <copyright file="ConnectionSettingsWriterTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

using System.Xml.Linq;

/// <summary>
/// Tests generation of the transient database configuration.
/// </summary>
public class ConnectionSettingsWriterTests
{
    /// <summary>Verifies every connection is loopback-only and uses the private port.</summary>
    [Test]
    public void WritesLoopbackConnectionsForEveryContext()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"openmu-connections-{Guid.NewGuid():N}.xml");
        try
        {
            new ConnectionSettingsWriter().Write(filePath, 55432, new LocalSecrets
            {
                DatabaseAdminPassword = "admin-secret",
                ConfigurationPassword = "config-secret",
                AccountPassword = "account-secret",
                FriendPassword = "friend-secret",
                GuildPassword = "guild-secret",
            });
            var document = XDocument.Load(filePath);
            var connectionStrings = document.Descendants().Where(element => element.Name.LocalName == "ConnectionString").Select(element => element.Value).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(connectionStrings, Has.Count.EqualTo(8));
                Assert.That(connectionStrings.All(value => value.Contains("127.0.0.1", StringComparison.Ordinal)), Is.True);
                Assert.That(connectionStrings.All(value => value.Contains("55432", StringComparison.Ordinal)), Is.True);
                Assert.That(connectionStrings.All(value => !value.Contains("127.127.127.127", StringComparison.Ordinal)), Is.True);
            });
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
