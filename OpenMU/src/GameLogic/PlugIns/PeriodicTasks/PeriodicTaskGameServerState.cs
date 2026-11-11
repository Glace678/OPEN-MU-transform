// <copyright file="PeriodicTaskGameServerState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.Threading;

/// <summary>
/// Allows forcing a periodic task to start only on a targeted <see cref="IGameContext"/>.
/// </summary>
internal interface IContextScopedPeriodicTaskPlugIn
{
    /// <summary>
    /// Forces the periodic task to start on the specified <paramref name="gameContext"/>.
    /// </summary>
    /// <param name="gameContext">The target game context.</param>
    void ForceStart(IGameContext gameContext);
}

/// <summary>
/// Game server state per event.
/// </summary>
public class PeriodicTaskGameServerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicTaskGameServerState"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    public PeriodicTaskGameServerState(IGameContext context)
    {
        this.Context = context;
    }

    /// <summary>
    /// Gets the context to which this state belongs.
    /// </summary>
    public IGameContext Context { get; }

    /// <summary>
    /// Gets or sets the next run in UTC.
    /// </summary>
    public DateTime NextRunUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp of when the last event run started.
    /// Used to prevent a new event from starting before the previous task duration has elapsed.
    /// </summary>
    public DateTime LastRunUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public PeriodicTaskState State { get; set; } = PeriodicTaskState.NotStarted;

    /// <summary>
    /// Gets the description about the state.
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// Gets the gate serializing periodic state transitions for this game context.
    /// </summary>
    internal SemaphoreSlim ExecutionGate { get; } = new(1, 1);

    /// <summary>
    /// Gets or sets a value indicating whether start was forced specifically for this game context.
    /// </summary>
    internal bool IsContextStartForced { get; set; }

    /// <summary>
    /// Gets or sets the last global force-start generation handled by this game context.
    /// </summary>
    internal long HandledForceStartGeneration { get; set; }
}