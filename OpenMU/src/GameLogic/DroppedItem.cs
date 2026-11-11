// <copyright file="DroppedItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Threading;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Pathfinding;
using Nito.AsyncEx;

/// <summary>
/// An item that got dropped on the ground of a map.
/// </summary>
public sealed class DroppedItem : AsyncDisposable, ILocateable
{
    private static readonly TimeSpan TimeUntilDropIsFree = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the pickup lock. Used to synchronize pickup requests from the players.
    /// </summary>
    private readonly AsyncLock _pickupLock = new();

    private readonly DateTime _dropTimestamp = DateTime.UtcNow;

    /// <summary>
    /// Indicates if the item was persistent (exists on the database) when it was dropped.
    /// If it wasn't and we clean it up, then we don't need to delete it.
    /// </summary>
    private readonly bool _wasItemPersisted;

    private Player? _dropper;

    private IEnumerable<object>? _owners;

    private Timer? _removeTimer;

    private bool _availableToPick = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="DroppedItem" /> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="position">The position where the item was dropped on the map.</param>
    /// <param name="map">The map.</param>
    /// <param name="dropper">The dropper.</param>
    public DroppedItem(Item item, Point position, GameMap map, Player dropper)
        : this(item, position, map, dropper, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DroppedItem" /> class.
    /// </summary>
    /// <param name="item">The item, which should be detached from any player persistence context.</param>
    /// <param name="position">The position where the item was dropped on the map.</param>
    /// <param name="map">The map.</param>
    /// <param name="dropper">The dropper.</param>
    /// <param name="owners">The owners.</param>
    /// <param name="wasItemPersisted">If set to <c>true</c>, the item was persisted before and exists on the database.</param>
    public DroppedItem(Item item, Point position, GameMap map, Player? dropper, IEnumerable<object>? owners, bool wasItemPersisted = false)
    {
        this.Item = item;
        this.Position = position;
        this.CurrentMap = map;
        this._dropper = dropper;
        this._owners = owners;
        this._wasItemPersisted = wasItemPersisted;
        this._removeTimer = new Timer(this.DisposeAndDelete, null, (int)map.ItemDropDuration.TotalMilliseconds, Timeout.Infinite);
    }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public Item Item { get; }

    /// <inheritdoc />
    public Point Position { get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public ushort Id { get; set; }

    /// <inheritdoc/>
    public GameMap CurrentMap { get; }

    /// <summary>
    /// Gets a value indicating whether the owner-pickup priority period is still active.
    /// </summary>
    public bool IsOwnerPickupPriorityActive => DateTime.UtcNow < this._dropTimestamp.Add(TimeUntilDropIsFree);

    /// <summary>
    /// Tries to pick the item up by the specified player.
    /// 尝试由指定玩家拾取地图上的掉落物。
    /// </summary>
    /// <param name="player">The player attempting to pick up the item / 尝试拾取掉落物的玩家.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: <c>true</c> if the item was successfully picked up or stacked; otherwise, <c>false</c>.
    /// - StackTarget: If success is <c>true</c> and this is not <c>null</c>, the dropped item was merged onto this existing inventory item.
    /// 返回包含拾取结果与堆叠目标的元组：Success 表示是否成功拾取；StackTarget 若非空则表示合并的目标物品。
    /// </returns>
    /// <remarks>
    /// Ownership and Anti-Theft Contract (GL-01 / R2-GL01) / 掉落物归属与防盗契约:
    /// 1. Availability: Atomically checks <c>_availableToPick</c> to prevent concurrent double-pickup.
    ///    可用性：原子检查可用标志，规避并发重复拾取。
    /// 2. Character Binding: If <see cref="ItemDefinition.IsBoundToCharacter"/> is true, only the owner can pick it up.
    ///    角色专属绑定：专属物品仅限拥有者拾取，非拥有者即使保护期结束亦不可拾取。
    /// 3. Ownership Window: Non-owners cannot pick up or stack items during the 10-second priority window (<see cref="TimeUntilDropIsFree"/>).
    ///    保护期机制：在 10 秒保护期倒计时内，严格阻断非拥有者的任何拾取与堆叠尝试。
    /// 4. Stack Precedence: Stacking onto inventory items is only permitted AFTER passing character binding and ownership guards.
    ///    堆叠时序：仅在通过所有权与绑定检查后，方可尝试与背包中的可堆叠物品合并。
    /// </remarks>
    public async ValueTask<(bool Success, Item? StackTarget)> TryPickUpByAsync(Player player)
    {
        Item? stackTarget = null;
        if (!this._availableToPick)
        {
            return (false, stackTarget);
        }

        if (this.Item.Definition!.IsBoundToCharacter && !this.IsPlayerAnOwner(player))
        {
            return (false, stackTarget);
        }

        if (!this.IsPlayerAnOwner(player)
            && DateTime.UtcNow < this._dropTimestamp.Add(TimeUntilDropIsFree))
        {
            return (false, stackTarget);
        }

        if (this.Item.IsStackable())
        {
            stackTarget = player.Inventory?.Items.FirstOrDefault(i => i.CanCompletelyStackOn(this.Item));
        }

        if (stackTarget != null)
        {
            if (await this.TryStackOnItemAsync(player, stackTarget).ConfigureAwait(false))
            {
                return (true, stackTarget);
            }

            return (false, stackTarget);
        }

        return (await this.TryPickUpAsync(player).ConfigureAwait(false), stackTarget);
    }

    /// <summary>
    /// Determines whether the specified player is an owner of this dropped item.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if the player is an owner; otherwise, <c>false</c>.</returns>
    public bool IsPlayerAnOwner(Player player)
    {
        return this._owners?.Contains(player) ?? true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Id}: {this.Item} at {this.CurrentMap.Definition.Name} ({this.Position})";
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        var timer = Interlocked.Exchange(ref this._removeTimer, null);
        if (timer != null)
        {
            await timer.DisposeAsync().ConfigureAwait(false);
        }

        await this.CurrentMap.RemoveAsync(this).ConfigureAwait(false);
        this._dropper = null;
        this._owners = null;

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Catching all Exceptions.")]
    private async void DisposeAndDelete(object? state)
    {
        var player = this._dropper;
        try
        {
            // Serialize expiry with pickup through the same lock: a pickup that is already in
            // progress sets _availableToPick=false, in which case the expired row must not be
            // deleted (otherwise the picked-up item would vanish from the database).
            using (await this._pickupLock.LockAsync().ConfigureAwait(false))
            {
                if (!this._availableToPick)
                {
                    return;
                }

                this._availableToPick = false;
                await this.DisposeAsync().ConfigureAwait(false);
            }

            if (player != null)
            {
                await this.DeleteItemAsync(player).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // we have to catch all errors, because it runs under a pooled thread without an additional safety net ;-)
            player?.Logger.LogError(ex, "Error during DroppedItem.DisposeAndDeleteAsync");
        }
    }

    private async ValueTask<bool> TryPickUpAsync(Player player)
    {
        player.Logger.LogDebug("Player {0} tries to pick up {1}", player, this);
        var slot = player.Inventory?.CheckInvSpace(this.Item);
        if (!slot.HasValue || slot < InventoryConstants.LastEquippableItemSlotIndex)
        {
            player.Logger.LogDebug("Inventory full, Player {0}, Item {1}", player, this);
            return false;
        }

        var itemWasTemporary = this.Item is TemporaryItem;
        using (await this._pickupLock.LockAsync())
        {
            if (!this._availableToPick)
            {
                player.Logger.LogDebug("Picked up by another player in the mean time, Player {0}, Item {1}", player, this);
                return false;
            }

            // Claim before any persistence work so a concurrent expiry timer cannot delete the
            // row we are about to attach/save.
            this._availableToPick = false;

            if (!itemWasTemporary)
            {
                // We already attach it here so that the next changes are in the context.
                player.PersistenceContext.Attach(this.Item);
            }

            if (!await player.Inventory!.AddItemAsync((byte)slot, this.Item).ConfigureAwait(false))
            {
                player.Logger.LogDebug("Item could not be added to the inventory, Player {0}, Item {1}", player, this);

                if (!itemWasTemporary)
                {
                    player.PersistenceContext.Detach(this.Item);
                }

                // Release the claim so the item can still expire or be picked by another player.
                this._availableToPick = true;
                return false;
            }

            if (itemWasTemporary)
            {
                // We set the item slot in a temporary item manually, so the further logic can report to the client where the item has been added.
                // If it's temporary, it has been converted into a persistent item before it was added to the inventory. So it's different instance.
                // If it's not temporary, this step is not required because the inventory already set the ItemSlot in the same instance we're holding here.
                this.Item.ItemSlot = (byte)slot;
            }
        }

        player.Logger.LogDebug("Item '{0}' was picked up by player '{1}' and added to his inventory.", this, player);
        await this.DisposeAsync().ConfigureAwait(false);

        return true;
    }

    private async ValueTask<bool> TryStackOnItemAsync(Player player, Item stackTarget)
    {
        player.Logger.LogDebug("Player {0} tries to pick up {1}, trying to add to an existing item at slot {2}", player, this, stackTarget.ItemSlot);
        Player? dropper;
        using (await this._pickupLock.LockAsync().ConfigureAwait(false))
        {
            if (!this._availableToPick || !stackTarget.CanCompletelyStackOn(this.Item))
            {
                player.Logger.LogDebug("Picked up by another player or stack target full in the mean time, Player {0}, Item {1}", player, this);
                return false;
            }

            var originalDurability = stackTarget.Durability;
            this._availableToPick = false;
            dropper = this._dropper;
            if (dropper != null && !await this.DeleteItemAsync(dropper).ConfigureAwait(false))
            {
                this._availableToPick = true;
                stackTarget.Durability = originalDurability;
                return false;
            }

            stackTarget.Durability = (byte)Math.Min((int)stackTarget.Durability + this.Item.Durability, byte.MaxValue);
        }

        player.Logger.LogInformation("Item '{0}' got picked up by player '{1}'. Durability of available stack {2} increased to {3}", this, player, stackTarget, stackTarget.Durability);
        await this.DisposeAsync().ConfigureAwait(false);
        if (player.GameContext.PlugInManager.GetPlugInPoint<PlugIns.IItemStackedPlugIn>() is { } itemStackedPlugIn)
        {
            await itemStackedPlugIn.ItemStackedAsync(player, this.Item, stackTarget).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask<bool> DeleteItemAsync(Player player)
    {
        player.Logger.LogDebug("Item '{0}' which was dropped by player '{1}' is getting deleted.", this, player);
        if (!this._wasItemPersisted)
        {
            return true;
        }

        try
        {
            var repositoryManager = player.GameContext.PersistenceContextProvider;

            // We could use here the persistence context of the dropper - but if it logged out and is not saving anymore, the deletion would not be saved.
            // So we use a new temporary persistence context instead.
            // We use a trade-context as it just focuses on the items. Otherwise, we would track a lot more items.
            using var context = repositoryManager.CreateNewTradeContext();
            context.Attach(this.Item);
            await context.DeleteAsync(this.Item).ConfigureAwait(false);
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return false;
            }
        }
        catch (Exception e)
        {
            player.Logger.LogWarning("Exception during deleting of the item {0}: {1}\n{2}", this, e.Message, e.StackTrace);
            return false;
        }

        player.GameContext.PlugInManager.GetPlugInPoint<IItemDestroyedPlugIn>()?.ItemDestroyed(this.Item);
        return true;
    }
}