// <copyright file="StateMachine.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.ComponentModel;
using System.Threading;
using MUnique.OpenMU.PlugIns;
using Nito.AsyncEx;

/// <summary>
/// A state machine.
/// </summary>
public class StateMachine
{
    private static long _nextInstanceId;

    /// <summary>
    /// The lock object for state transitions.
    /// </summary>
    private readonly AsyncLock _asyncLock = new();

    /// <summary>
    /// A process-wide unique, monotonic identifier used to establish a canonical lock order
    /// when several state machines must be locked together (e.g. trade settlement), so that
    /// concurrent operations cannot acquire them in opposite orders and deadlock (AB/BA).
    /// </summary>
    private readonly long _instanceId = Interlocked.Increment(ref _nextInstanceId);

    /// <summary>
    /// Gets the process-wide unique ordering identifier of this state machine.
    /// </summary>
    internal long InstanceId => this._instanceId;

    /// <summary>
    /// A cancel event args object, which is getting reused.
    /// </summary>
    private readonly StateChangeEventArgs _cachedCancelEventArgs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachine"/> class.
    /// </summary>
    /// <param name="initialState">The initial state.</param>
    public StateMachine(State initialState)
    {
        this.CurrentState = initialState;
    }

    /// <summary>
    /// Event that fires just before the state changes.
    /// </summary>
    public event AsyncEventHandler<StateChangeEventArgs>? StateChanges;

    /// <summary>
    /// Event that fires after the state have changed.
    /// </summary>
    public event AsyncEventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public State CurrentState { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the state machine is in a finished state, that means that no further state changes are possible.
    /// </summary>
    public bool Finished => this.CurrentState?.PossibleTransitions is null || this.CurrentState.PossibleTransitions.Count == 0;

    /// <summary>
    /// Tries to advance the state to <paramref name="nextState"/>.
    /// </summary>
    /// <param name="nextState">The state to advance to.</param>
    /// <returns>The success.</returns>
    public async ValueTask<bool> TryAdvanceToAsync(State nextState)
    {
        if (this.CurrentState?.PossibleTransitions is null)
        {
            return false;
        }

        State previousState;
        using (await this._asyncLock.LockAsync().ConfigureAwait(false))
        {
            if (this.CurrentState?.PossibleTransitions is not { } possibleTransitions)
            {
                return false;
            }

            if (!possibleTransitions.Contains(nextState) || !await this.OnStateChangingAsync(nextState).ConfigureAwait(false))
            {
                return false;
            }

            previousState = this.CurrentState;
            this.CurrentState = nextState;
        }

        await this.OnStateChangedAsync(previousState, nextState).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Tries to start a "transaction" to advance the state to <paramref name="nextState"/>.
    /// </summary>
    /// <param name="nextState">The state to advance to.</param>
    /// <returns>The state change context. On disposal of this object, the state change is getting completed.</returns>
    public async ValueTask<StateChangeContext> TryBeginAdvanceToAsync(State nextState)
    {
        var lockRelease = await this._asyncLock.LockAsync().ConfigureAwait(false);
        try
        {
            State? previousState = null;
            var allowed = (this.CurrentState.PossibleTransitions?.Contains(nextState) ?? false)
                          && await this.OnStateChangingAsync(nextState).ConfigureAwait(false);
            return new StateChangeContext(
                lockRelease,
                () =>
                {
                    previousState = this.CurrentState;
                    this.CurrentState = nextState;
                },
                async () =>
                {
                    if (previousState is not null)
                    {
                        await this.OnStateChangedAsync(previousState, nextState).ConfigureAwait(false);
                    }
                })
            {
                Allowed = allowed,
            };
        }
        catch
        {
            lockRelease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Calls the StateChanged-Event.
    /// </summary>
    private async ValueTask OnStateChangedAsync(State previousState, State currentState)
    {
        await this.StateChanged.SafeInvokeAsync(new StateChangedEventArgs(previousState, currentState)).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the StateChanges-Event.
    /// </summary>
    /// <param name="nextState">The next state.</param>
    /// <returns><c>True</c>, if all event handlers did not set <see cref="CancelEventArgs"/> to <c>true</c>; Otherwise, <c>false</c>.</returns>
    private async ValueTask<bool> OnStateChangingAsync(State nextState)
    {
        if (this.StateChanges != null)
        {
            this._cachedCancelEventArgs.Cancel = false;
            this._cachedCancelEventArgs.NextState = nextState;
            await this.StateChanges.SafeInvokeAsync(this._cachedCancelEventArgs).ConfigureAwait(false);
            return !this._cachedCancelEventArgs.Cancel;
        }

        return true;
    }

    /// <summary>
    /// The state change context for more complex state changes.
    /// On disposal of this object, the state change is getting completed.
    /// </summary>
    public sealed class StateChangeContext : IAsyncDisposable
    {
        /// <summary>
        /// The lock release of the acquired lock of the state machine.
        /// </summary>
        private readonly IDisposable _lockRelease;

        /// <summary>
        /// The synchronous action executed while holding the state machine lock.
        /// </summary>
        private readonly Action? _commitStateAction;

        /// <summary>
        /// The action which gets executed when the state change is completed.
        /// </summary>
        private readonly Func<ValueTask> _finishAction;

        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateChangeContext"/> class.
        /// </summary>
        /// <param name="lockRelease">The lock object of the state machine, which is in the locked state.</param>
        /// <param name="finishAction">The action which should get executed when the state change is completed.</param>
        public StateChangeContext(IDisposable lockRelease, Func<ValueTask> finishAction)
            : this(lockRelease, null, finishAction)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StateChangeContext"/> class.
        /// </summary>
        /// <param name="lockRelease">The lock object of the state machine, which is in the locked state.</param>
        /// <param name="commitStateAction">The synchronous state mutation executed before releasing the lock.</param>
        /// <param name="finishAction">The async notification executed after releasing the lock.</param>
        internal StateChangeContext(IDisposable lockRelease, Action? commitStateAction, Func<ValueTask> finishAction)
        {
            this._lockRelease = lockRelease;
            this._commitStateAction = commitStateAction;
            this._finishAction = finishAction;
        }

        /// <summary>
        /// Gets a value indicating whether a state change is allowed.
        /// </summary>
        public bool Allowed { get; internal set; }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this._disposed, 1) != 0)
            {
                return;
            }

            var allowed = this.Allowed;
            try
            {
                if (allowed)
                {
                    this._commitStateAction?.Invoke();
                }
            }
            finally
            {
                this._lockRelease.Dispose();
            }

            if (allowed)
            {
                await this._finishAction().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The state change event args, including the next state.
    /// </summary>
    public class StateChangeEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Gets or sets the next state.
        /// </summary>
        public State? NextState { get; set; }
    }

    /// <summary>
    /// The event args for <see cref="StateMachine.StateChanged"/>.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="previousState">State of the previous.</param>
        /// <param name="currentState">State of the current state.</param>
        public StateChangedEventArgs(State previousState, State currentState)
        {
            this.PreviousState = previousState;
            this.CurrentState = currentState;
        }

        /// <summary>
        /// Gets the state of the previous state.
        /// </summary>
        public State PreviousState { get; }

        /// <summary>
        /// Gets the state of the current state.
        /// </summary>
        public State CurrentState { get; }

        /// <summary>
        /// Gets the state of the current state.
        /// </summary>
        /// <remarks>Compatibility alias for the previously public name; use <see cref="CurrentState"/>.</remarks>
        [Obsolete("Use CurrentState. Scheduled for removal.")]
        public State CurrentStateState => this.CurrentState;
    }
}