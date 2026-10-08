// <copyright file="PeriodicTaskBasePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Base class for periodic task plugins.
/// </summary>
/// <typeparam name="TConfiguration">Configuration type.</typeparam>
/// <typeparam name="TState">State type.</typeparam>
public abstract class PeriodicTaskBasePlugIn<TConfiguration, TState> : IPeriodicTaskPlugIn, ISupportCustomConfiguration<TConfiguration>, IDisposable
    where TConfiguration : PeriodicTaskConfiguration
    where TState : PeriodicTaskGameServerState
{
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<IGameContext, TState>> States = new();

    private readonly List<IGameContext> _handledContexts = [];

    private readonly object _handledContextsLock = new();

    private int _isDisposed;

    private bool _isStartForced = false;

    /// <summary>
    /// Gets or sets configuration for periodic invasion.
    /// </summary>
    public TConfiguration? Configuration { get; set; }

    /// <summary>
    /// Forces to start the task on the next start check.
    /// </summary>
    public void ForceStart()
    {
        this._isStartForced = true;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var logger = gameContext.LoggerFactory.CreateLogger(this.GetType().Name);
        using var scope = logger.BeginScope(gameContext);

        var state = this.GetStateByGameContext(gameContext);

        if (state.NextRunUtc > DateTime.UtcNow)
        {
            return;
        }

        var configuration = this.Configuration;

        if (configuration is null && this is ISupportDefaultCustomConfiguration defaultConfigSupporter)
        {
            logger.LogWarning("{description} ({gameContext}):configuration is not set. Using default configuration.", state.Description, gameContext);
            this.Configuration = configuration = defaultConfigSupporter.CreateDefaultConfig() as TConfiguration;
        }

        if (configuration is null)
        {
            logger.LogError("{description} ({gameContext}):no configuration available; can't execute task plugin.", state.Description, gameContext);
            return;
        }

        switch (state.State)
        {
            case PeriodicTaskState.NotStarted:
                {
                    if (!this.IsItTimeToStart(gameContext))
                    {
                        return;
                    }

                    if (this.IsPreviousEventStillRunning(state))
                    {
                        this._isStartForced = false;
                        return;
                    }

                    this._isStartForced = false;
                    state.NextRunUtc = DateTime.UtcNow.Add(configuration.PreStartMessageDelay);
                    await this.OnPrepareEventAsync(state).ConfigureAwait(false);
                    state.State = PeriodicTaskState.Prepared;
                    await this.OnPreparedAsync(state).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(state.Description))
                    {
                        logger.LogDebug("{description} ({gameContext}): event prepared", state.Description, gameContext);
                    }

                    break;
                }

            case PeriodicTaskState.Prepared:
                {
                    state.NextRunUtc = DateTime.UtcNow.Add(configuration.TaskDuration);
                    state.State = PeriodicTaskState.Started;
                    state.LastRunUtc = DateTime.UtcNow;

                    await this.OnStartedAsync(state).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(state.Description))
                    {
                        logger.LogDebug("{description} ({gameContext}): event started", state.Description, gameContext);
                    }

                    break;
                }

            case PeriodicTaskState.Started:
                {
                    state.State = PeriodicTaskState.NotStarted;

                    await this.OnFinishedAsync(state).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(state.Description))
                    {
                        logger.LogDebug("{description} ({gameContext}): event finished", state.Description, gameContext);
                    }

                    break;
                }

            default:
                throw new NotImplementedException("Unknown state.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._isDisposed, 1) != 0)
        {
            return;
        }

        List<IGameContext> contexts;
        lock (this._handledContextsLock)
        {
            contexts = this._handledContexts.ToList();
            this._handledContexts.Clear();
        }

        if (!States.TryGetValue(this.GetType(), out var statesPerType))
        {
            return;
        }

        foreach (var gameContext in contexts)
        {
            statesPerType.TryRemove(gameContext, out _);
        }
    }

    /// <summary>
    /// Gets a value indicating whether if it's the right time to start the task.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>
    ///   <c>true</c> if it's the right time to start the task; otherwise, <c>false</c>.
    /// </returns>
    protected virtual bool IsItTimeToStart(IGameContext gameContext)
    {
        return this._isStartForced || (this.Configuration?.IsItTimeToStart(gameContext.ServerTimeZone) ?? false);
    }

    /// <summary>
    /// Determines whether the previous event run is still within its configured task duration.
    /// Prevents a new event from starting before the previous one has fully elapsed.
    /// </summary>
    /// <param name="state">The current task state.</param>
    /// <returns><c>true</c> if the previous event duration has not elapsed yet; otherwise <c>false</c>.</returns>
    protected virtual bool IsPreviousEventStillRunning(TState state)
        => state.LastRunUtc != DateTime.MinValue && state.LastRunUtc.Add(this.Configuration?.TaskDuration ?? TimeSpan.Zero) > DateTime.UtcNow;

    /// <summary>
    /// Called when the task should be prepared before starting it.
    /// </summary>
    /// <param name="state">The state.</param>
    protected abstract ValueTask OnPrepareEventAsync(TState state);

    /// <summary>
    /// Creates the state for the given context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The created state object.</returns>
    protected abstract TState CreateState(IGameContext gameContext);

    /// <summary>
    /// Get a unique state per GameContext.
    /// </summary>
    /// <param name="gameContext">GameContext.</param>
    protected TState GetStateByGameContext(IGameContext gameContext)
    {
        if (Volatile.Read(ref this._isDisposed) != 0)
        {
            throw new ObjectDisposedException(this.GetType().Name);
        }

        var type = this.GetType();

        var statesPerType = States.GetOrAdd(type, newType => new());

        var state = statesPerType.GetOrAdd(gameContext, _ => this.CreateState(gameContext));

        lock (this._handledContextsLock)
        {
            if (!this._handledContexts.Contains(gameContext))
            {
                this._handledContexts.Add(gameContext);
            }
        }

        return state;
    }

    /// <summary>
    /// Calls after the state changed to Prepared.
    /// </summary>
    /// <param name="state">The state.</param>
    protected abstract ValueTask OnPreparedAsync(TState state);

    /// <summary>
    /// Calls after the state changed to Started.
    /// </summary>
    /// <param name="state">State.</param>
    protected abstract ValueTask OnStartedAsync(TState state);

    /// <summary>
    /// Calls after the state changed to Finished.
    /// </summary>
    /// <param name="state">State.</param>
    protected abstract ValueTask OnFinishedAsync(TState state);
}