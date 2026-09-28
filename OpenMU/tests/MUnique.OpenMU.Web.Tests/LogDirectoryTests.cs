// <copyright file="LogDirectoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.IO;
using MUnique.OpenMU.Web.AdminPanel;

/// <summary>
/// Tests the admin panel's packaged log directory integration.
/// </summary>
public class LogDirectoryTests
{
    private const string LogDirectoryEnvironmentVariable = "OPENMU_LOG_DIRECTORY";

    /// <summary>Uses and creates the explicit local-launcher log directory.</summary>
    [Test]
    [NonParallelizable]
    public void ExplicitLogDirectoryIsCreatedAndServed()
    {
        var originalValue = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        var logDirectory = Path.Combine(Path.GetTempPath(), $"openmu-logs-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable(LogDirectoryEnvironmentVariable, logDirectory);
            using var provider = WebApplicationExtensions.CreateLogFileProvider();

            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(logDirectory), Is.True);
                Assert.That(
                    Path.TrimEndingDirectorySeparator(provider.Root),
                    Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(logDirectory))).IgnoreCase);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(LogDirectoryEnvironmentVariable, originalValue);
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }
}
