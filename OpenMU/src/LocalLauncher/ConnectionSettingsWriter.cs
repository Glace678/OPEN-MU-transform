// <copyright file="ConnectionSettingsWriter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Data.Common;
using System.Xml.Linq;

/// <summary>
/// Creates the transient OpenMU database connection configuration.
/// </summary>
public sealed class ConnectionSettingsWriter
{
    private static readonly XNamespace Namespace = "http://www.munique.net/ConnectionSettings";

    /// <summary>
    /// Writes the OpenMU connection settings for the private local PostgreSQL instance.
    /// </summary>
    /// <param name="filePath">The destination file.</param>
    /// <param name="port">The PostgreSQL port.</param>
    /// <param name="secrets">The database secrets.</param>
    public void Write(string filePath, int port, LocalSecrets secrets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var connections = new[]
        {
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.EntityDataContext", "postgres", secrets.DatabaseAdminPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.AdminAuth.AdminPanelContext", "postgres", secrets.DatabaseAdminPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.TypedContext", "postgres", secrets.DatabaseAdminPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.ConfigurationContext", "config", secrets.ConfigurationPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.AccountContext", "account", secrets.AccountPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.TradeContext", "account", secrets.AccountPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.FriendContext", "friend", secrets.FriendPassword, port),
            CreateConnection("MUnique.OpenMU.Persistence.EntityFramework.GuildContext", "guild", secrets.GuildPassword, port),
        };
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Namespace + "ConnectionSettings", new XElement(Namespace + "Connections", connections)));
        document.Save(filePath);
    }

    private static XElement CreateConnection(string contextType, string userName, string password, int port)
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Server"] = "127.0.0.1",
            ["Port"] = port,
            ["User Id"] = userName,
            ["Password"] = password,
            ["Database"] = "openmu",
            ["Command Timeout"] = 120,
        };

        return new XElement(
            Namespace + "Connection",
            new XElement(Namespace + "ContextTypeName", contextType),
            new XElement(Namespace + "ConnectionString", connectionString.ConnectionString),
            new XElement(Namespace + "DatabaseEngine", "Npgsql"));
    }
}
