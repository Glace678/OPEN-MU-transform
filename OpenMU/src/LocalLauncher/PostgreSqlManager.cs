// <copyright file="PostgreSqlManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Initializes and controls the private PostgreSQL instance.
/// </summary>
public sealed class PostgreSqlManager
{
    private readonly LocalPaths _paths;
    private readonly PostgreSqlExecutionPaths _executionPaths;
    private readonly LocalStackSettings _settings;
    private readonly ProcessRunner _processRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlManager"/> class.
    /// </summary>
    /// <param name="paths">The package paths.</param>
    /// <param name="executionPaths">The verified ASCII PostgreSQL execution paths.</param>
    /// <param name="settings">The local settings.</param>
    /// <param name="processRunner">The process runner.</param>
    internal PostgreSqlManager(LocalPaths paths, PostgreSqlExecutionPaths executionPaths, LocalStackSettings settings, ProcessRunner processRunner)
    {
        this._paths = paths;
        this._executionPaths = executionPaths;
        this._settings = settings;
        this._processRunner = processRunner;
    }

    /// <summary>Gets the PostgreSQL process identifier from the data directory, if available.</summary>
    public int? ProcessId
    {
        get
        {
            var pidFile = Path.Combine(this._paths.PostgreSqlDataDirectory, "postmaster.pid");
            return File.Exists(pidFile) && int.TryParse(File.ReadLines(pidFile).FirstOrDefault(), out var processId)
                ? processId
                : null;
        }
    }

    /// <summary>
    /// Determines whether the managed PostgreSQL cluster is running.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when pg_ctl reports a running server.</returns>
    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this._executionPaths.PgCtlExecutable)
            || !File.Exists(Path.Combine(this._paths.PostgreSqlDataDirectory, "PG_VERSION")))
        {
            return false;
        }

        this._executionPaths.Validate();
        var result = await this._processRunner.RunAsync(
            this._executionPaths.PgCtlExecutable,
            new[] { "status", "--pgdata", this._executionPaths.DataDirectory },
            this._executionPaths.RuntimeDirectory,
            null,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return false;
        }

        this.ValidateRunningEndpoint();
        return true;
    }

    /// <summary>Checks whether a saved game database needs backing up before conversion.</summary>
    /// <param name="password">The private database administrator password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the game database exists.</returns>
    public async Task<bool> HasGameDatabaseAsync(string password, CancellationToken cancellationToken)
    {
        this._executionPaths.Validate();
        var result = await this._processRunner.RunAsync(
            Path.Combine(this._executionPaths.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("psql")),
            new[]
            {
                "--host", "127.0.0.1", "--port", this._settings.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--username", "postgres", "--dbname", "postgres", "--no-psqlrc", "--tuples-only", "--no-align",
                "--set", "ON_ERROR_STOP=1", "--command", "SELECT 1 FROM pg_database WHERE datname = 'openmu'",
            },
            this._executionPaths.RuntimeDirectory,
            new Dictionary<string, string?> { ["PGPASSWORD"] = password },
            cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess("Check saved game database");
        return result.StandardOutput.Trim() == "1";
    }

    /// <summary>
    /// Initializes the database cluster when needed and starts PostgreSQL.
    /// </summary>
    /// <param name="adminPassword">The database administrator password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartAsync(string adminPassword, CancellationToken cancellationToken)
    {
        this.ValidateRuntime();
        if (!File.Exists(Path.Combine(this._paths.PostgreSqlDataDirectory, "PG_VERSION")))
        {
            await this.InitializeClusterAsync(adminPassword, cancellationToken).ConfigureAwait(false);
        }

        this._executionPaths.Validate();
        var result = await this._processRunner.RunWithDetachedDescendantAsync(
            this._executionPaths.PgCtlExecutable,
            new[]
            {
                "start",
                "--pgdata", this._executionPaths.DataDirectory,
                "--log", Path.Combine(this._executionPaths.LogsDirectory, "postgresql.log"),
                "--wait",
                "--timeout", this._settings.DatabaseStartupTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--options", $"-p {this._settings.DatabasePort} -h 127.0.0.1",
            },
            this._executionPaths.RuntimeDirectory,
            null,
            cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess("启动 PostgreSQL");
        await this.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops PostgreSQL with fast shutdown, allowing it to checkpoint and recover cleanly.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(this._paths.PostgreSqlDataDirectory, "postmaster.pid")))
        {
            return;
        }

        this._executionPaths.Validate();
        var result = await this._processRunner.RunAsync(
            this._executionPaths.PgCtlExecutable,
            new[] { "stop", "--pgdata", this._executionPaths.DataDirectory, "--mode", "fast", "--wait", "--timeout", "60" },
            this._executionPaths.RuntimeDirectory,
            null,
            cancellationToken).ConfigureAwait(false);
        result.EnsureSuccess("停止 PostgreSQL");
    }

    private async Task InitializeClusterAsync(string adminPassword, CancellationToken cancellationToken)
    {
        if (Directory.EnumerateFileSystemEntries(this._paths.PostgreSqlDataDirectory).Any())
        {
            throw new InvalidDataException("PostgreSQL 数据目录不为空，却缺少 PG_VERSION 标记。请先移走该目录或还原有效备份。");
        }

        var passwordFileName = $"initdb-{Guid.NewGuid():N}.pw";
        var passwordFile = Path.Combine(this._paths.KeysDirectory, passwordFileName);
        var executionPasswordFile = Path.Combine(this._executionPaths.KeysDirectory, passwordFileName);
        await File.WriteAllTextAsync(passwordFile, adminPassword, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        try
        {
            this._executionPaths.Validate();
            var result = await this._processRunner.RunAsync(
                this._executionPaths.InitDbExecutable,
                new[]
                {
                    "--pgdata", this._executionPaths.DataDirectory,
                    "--encoding", "UTF8",
                    "--locale", "C",
                    "--auth-host", "scram-sha-256",
                    "--auth-local", "scram-sha-256",
                    "--username", "postgres",
                    "--pwfile", executionPasswordFile,
                },
                this._executionPaths.RuntimeDirectory,
                null,
                cancellationToken).ConfigureAwait(false);
            result.EnsureSuccess("初始化 PostgreSQL");
        }
        finally
        {
            File.Delete(passwordFile);
        }

        await File.AppendAllLinesAsync(
            Path.Combine(this._paths.PostgreSqlDataDirectory, "postgresql.conf"),
            new[]
            {
                string.Empty,
                "# OpenMU-Local managed settings",
                "listen_addresses = '127.0.0.1'",
                $"port = {this._settings.DatabasePort}",
                "password_encryption = 'scram-sha-256'",
            },
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllLinesAsync(
            Path.Combine(this._paths.PostgreSqlDataDirectory, "pg_hba.conf"),
            new[]
            {
                "# OpenMU-Local: no remote database access.",
                "host all all 127.0.0.1/32 scram-sha-256",
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(this._settings.DatabaseStartupTimeoutSeconds);
        do
        {
            this._executionPaths.Validate();
            var result = await this._processRunner.RunAsync(
                this._executionPaths.PgIsReadyExecutable,
                new[] { "--host", "127.0.0.1", "--port", this._settings.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                this._executionPaths.RuntimeDirectory,
                null,
                cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                return;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        while (DateTime.UtcNow < timeoutAt);

        throw new TimeoutException("PostgreSQL 未在设定的超时时间内就绪。");
    }

    private void ValidateRuntime()
    {
        this._executionPaths.Validate();
        foreach (var executable in new[] { this._executionPaths.InitDbExecutable, this._executionPaths.PgCtlExecutable, this._executionPaths.PgIsReadyExecutable })
        {
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("随包提供的 PostgreSQL 运行库不完整。", executable);
            }
        }
    }

    private void ValidateRunningEndpoint()
    {
        var pidFile = Path.Combine(this._paths.PostgreSqlDataDirectory, "postmaster.pid");
        var lines = File.ReadAllLines(pidFile);
        if (lines.Length < 6
            || !int.TryParse(lines[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
            || port != this._settings.DatabasePort
            || !string.Equals(lines[5].Trim(), "127.0.0.1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("受管理的 PostgreSQL 进程没有仅监听设定的本机回环地址。");
        }
    }
}
