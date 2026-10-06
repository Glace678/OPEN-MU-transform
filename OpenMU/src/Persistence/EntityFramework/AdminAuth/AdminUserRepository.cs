// <copyright file="AdminUserRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.AdminAuth;
using Nito.AsyncEx;

/// <summary>
/// Implementation of the <see cref="IAdminUserRepository"/> which stores the users
/// in the <c>admin</c> schema of the configured PostgreSQL database.
/// </summary>
public class AdminUserRepository : IAdminUserRepository
{
    /// <summary>
    /// The time after which a connection attempt to the database server is given up.
    /// </summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The time after which the creation of the schema is given up.
    /// </summary>
    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The time to wait before the storage is probed again after a failed attempt.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<AdminUserRepository> _logger;
    private readonly AsyncLock _storageLock = new();
    private bool _isStorageReady;
    private DateTime _nextProbeAt = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminUserRepository"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AdminUserRepository(ILogger<AdminUserRepository> logger)
    {
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> EnsureStorageAsync(CancellationToken cancellationToken = default)
    {
        if (this._isStorageReady)
        {
            return true;
        }

        // The authorization of every request asks whether a user exists, so a database which is not
        // reachable must not be retried on each of them - otherwise the whole panel waits for a
        // connection which is going to time out anyway.
        if (DateTime.UtcNow < this._nextProbeAt)
        {
            return false;
        }

        using var l = await this._storageLock.LockAsync(cancellationToken).ConfigureAwait(false);
        if (this._isStorageReady)
        {
            return true;
        }

        if (DateTime.UtcNow < this._nextProbeAt)
        {
            return false;
        }

        try
        {
            await using var context = new AdminPanelContext();

            // Connecting is checked separately and with a short timeout, because the configured
            // command timeout of the connection string is far too long to block a request on.
            bool canConnect;
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(ConnectTimeout);
                canConnect = await context.Database.CanConnectAsync(connectCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // This is an expected state before the database server is reachable or the database has been created.
                // The admin panel then falls back to the configured bootstrap user.
                this._logger.LogInformation(ex, "The admin user storage is not reachable (yet).");
                canConnect = false;
            }

            if (canConnect)
            {
                try
                {
                    using var migrationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    migrationCts.CancelAfter(MigrationTimeout);
                    await context.Database.MigrateAsync(migrationCts.Token).ConfigureAwait(false);
                    this._isStorageReady = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // The database is reachable but the migration failed/timed out; this must be visible
                    // instead of silently running against an empty (bootstrap) storage.
                    this._logger.LogWarning(ex, "Failed to migrate the admin user storage; it will be retried.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
            if (!this._isStorageReady)
            {
                this._nextProbeAt = DateTime.UtcNow + RetryDelay;
            }
        }

        return this._isStorageReady;
    }

    /// <summary>
    /// Explicitly migrates the admin schema during application startup instead of lazily on the
    /// first authentication request. A failure is logged as an error and rethrown so that the host
    /// can fail fast (and restart) instead of silently running against an un-migrated storage.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async ValueTask InitializeStorageAsync(CancellationToken cancellationToken = default)
    {
        using var l = await this._storageLock.LockAsync(cancellationToken).ConfigureAwait(false);
        if (this._isStorageReady)
        {
            return;
        }

        await using var context = new AdminPanelContext();
        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            this._isStorageReady = true;
            this._nextProbeAt = DateTime.MinValue;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to migrate the admin user storage during startup.");
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        await using var context = new AdminPanelContext();
        return await context.AdminUsers.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IList<AdminUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return new List<AdminUser>();
        }

        await using var context = new AdminPanelContext();
        return await context.AdminUsers
            .AsNoTracking()
            .OrderBy(u => u.LoginName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await using var context = new AdminPanelContext();
        return await context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<AdminUser?> GetByNormalizedLoginNameAsync(string normalizedLoginName, CancellationToken cancellationToken = default)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await using var context = new AdminPanelContext();
        return await context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedLoginName == normalizedLoginName, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        context.AdminUsers.Add(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        context.AdminUsers.Update(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        await this.EnsureAvailableStorageAsync(cancellationToken).ConfigureAwait(false);

        await using var context = new AdminPanelContext();
        context.AdminUsers.Remove(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAvailableStorageAsync(CancellationToken cancellationToken)
    {
        if (!await this.EnsureStorageAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The admin user storage is not available. Please check the database connection.");
        }
    }
}
