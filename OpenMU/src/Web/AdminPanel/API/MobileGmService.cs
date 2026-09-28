// <copyright file="MobileGmService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using Nito.AsyncEx;

/// <summary>
/// Performs the restricted, online-only operations exposed to the packaged mobile GM application.
/// </summary>
public sealed class MobileGmService
{
    private const int MaximumRememberedGrantOperations = 4096;

    // The MU inventory money field is a signed 32-bit value; the classic cap is 2 billion Zen.
    private const long MaximumMoney = 2_000_000_000L;

    private readonly IServiceProvider _services;
    private readonly ILogger<MobileGmService> _logger;
    private readonly Dictionary<Guid, GrantOperation> _grantOperations = new();
    private readonly Dictionary<Guid, ZenOperation> _zenOperations = new();
    private readonly object _grantOperationsLock = new();

    /// <summary>Initializes a new instance of the <see cref="MobileGmService"/> class.</summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="logger">The logger.</param>
    public MobileGmService(IServiceProvider services, ILogger<MobileGmService> logger)
    {
        this._services = services;
        this._logger = logger;
    }

    /// <summary>Gets the local account and its currently selected online characters.</summary>
    /// <returns>The mobile status response.</returns>
    public async ValueTask<MobileGmStatusResponse> GetStatusAsync()
    {
        var accountName = this.GetAccountName();
        var servers = this.GetServers();
        var serverReady = servers.Any(server => server.ServerState == ServerState.Started);
        var characters = new Dictionary<Guid, MobileGmCharacter>();
        if (accountName.Length > 0)
        {
            foreach (var context in GetStartedContexts(servers))
            {
                foreach (var player in await context.GetPlayersAsync().ConfigureAwait(false))
                {
                    if (IsAccountPlayer(player, accountName) && player.SelectedCharacter is { } character)
                    {
                        characters[character.Id] = new MobileGmCharacter(character.Id, character.Name, player.Level, player.Money);
                    }
                }
            }
        }

        return new MobileGmStatusResponse(accountName, serverReady, characters.Values.OrderBy(character => character.Name).ToArray());
    }

    /// <summary>Searches safe item definitions of the running game configuration.</summary>
    /// <param name="query">The optional name or group/number query.</param>
    /// <returns>The item search response, limited to 100 entries.</returns>
    public MobileGmItemsResponse SearchItems(string? query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var items = GetStartedContexts(this.GetServers())
            .SelectMany(context => context.Configuration.Items)
            .Where(definition => !definition.IsQuestItem && !definition.IsBoundToCharacter)
            .GroupBy(definition => (definition.Group, definition.Number))
            .Select(group => group.First())
            .Select(definition => CreateItemSummary(definition))
            .Where(item => normalizedQuery.Length == 0
                           || item.Name.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)
                           || $"{item.Group}:{item.Number}".Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Group)
            .ThenBy(item => item.Number)
            .Take(100)
            .ToArray();
        return new MobileGmItemsResponse(items);
    }

    /// <summary>Grants an item to an eligible online character, idempotently per request identifier.</summary>
    /// <param name="request">The grant request.</param>
    /// <returns>The result.</returns>
    public Task<MobileGmGrantResponse> GrantItemAsync(MobileGmGrantRequest request)
    {
        var validationError = ValidateRequest(request, out var requestId, out var characterId);
        if (validationError is not null)
        {
            return Task.FromResult(new MobileGmGrantResponse(false, validationError));
        }

        GrantOperation operation;
        lock (this._grantOperationsLock)
        {
            if (!this._grantOperations.TryGetValue(requestId, out operation!))
            {
                if (this._grantOperations.Count >= MaximumRememberedGrantOperations)
                {
                    return Task.FromResult(new MobileGmGrantResponse(false, "幂等请求记录已满，请重启本地服务器后重试。"));
                }

                operation = new GrantOperation(
                    request,
                    new AsyncLazy<MobileGmGrantResponse>(() => this.GrantItemCoreAsync(request, characterId)));
                this._grantOperations[requestId] = operation;
            }
        }

        return operation.Request == request
            ? operation.Result.Task
            : Task.FromResult(new MobileGmGrantResponse(false, "requestId 已被其他请求使用。"));
    }

    /// <summary>Adds Zen (money) to an eligible online character, idempotently per request identifier.</summary>
    /// <param name="request">The Zen grant request.</param>
    /// <returns>The result.</returns>
    public Task<MobileGmGrantResponse> GrantZenAsync(MobileGmZenRequest request)
    {
        var validationError = ValidateZenRequest(request, out var requestId, out var characterId, out var amount);
        if (validationError is not null)
        {
            return Task.FromResult(new MobileGmGrantResponse(false, validationError));
        }

        ZenOperation operation;
        lock (this._grantOperationsLock)
        {
            if (!this._zenOperations.TryGetValue(requestId, out operation!))
            {
                if (this._grantOperations.Count + this._zenOperations.Count >= MaximumRememberedGrantOperations)
                {
                    return Task.FromResult(new MobileGmGrantResponse(false, "幂等请求记录已满，请重启本地服务器后重试。"));
                }

                operation = new ZenOperation(
                    request,
                    new AsyncLazy<MobileGmGrantResponse>(() => this.GrantZenCoreAsync(characterId, amount)));
                this._zenOperations[requestId] = operation;
            }
        }

        return operation.Request == request
            ? operation.Result.Task
            : Task.FromResult(new MobileGmGrantResponse(false, "requestId 已被其他请求使用。"));
    }

    /// <summary>Validates the pure, context-independent portion of a grant request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="requestId">The parsed request identifier.</param>
    /// <param name="characterId">The parsed character identifier.</param>
    /// <returns>An error message, or <c>null</c> when valid.</returns>
    internal static string? ValidateRequest(MobileGmGrantRequest? request, out Guid requestId, out Guid characterId)
    {
        requestId = Guid.Empty;
        characterId = Guid.Empty;
        if (request is null)
        {
            return "请求内容不能为空。";
        }

        if (!Guid.TryParse(request.RequestId, out requestId) || requestId == Guid.Empty)
        {
            return "requestId 必须是非空 UUID。";
        }

        if (!Guid.TryParse(request.CharacterId, out characterId) || characterId == Guid.Empty)
        {
            return "characterId 必须是非空 UUID。";
        }

        if (request.Group is < 0 or > byte.MaxValue || request.Number is < 0 or > short.MaxValue)
        {
            return "物品编号无效。";
        }

        if (request.Level is < 0 or > byte.MaxValue)
        {
            return "物品等级无效。";
        }

        if (request.AdditionalOptionLevel is < 0 or > 4)
        {
            return "追加选项等级必须在 0 到 4 之间。";
        }

        if (request.ExcellentMask < 0)
        {
            return "卓越属性掩码无效。";
        }

        return request.Quantity is < 1 or > 10 ? "发放数量必须在 1 到 10 之间。" : null;
    }

    /// <summary>Validates the pure, context-independent portion of a Zen grant request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="requestId">The parsed request identifier.</param>
    /// <param name="characterId">The parsed character identifier.</param>
    /// <param name="amount">The validated positive Zen amount.</param>
    /// <returns>An error message, or <c>null</c> when valid.</returns>
    internal static string? ValidateZenRequest(MobileGmZenRequest? request, out Guid requestId, out Guid characterId, out long amount)
    {
        requestId = Guid.Empty;
        characterId = Guid.Empty;
        amount = 0;
        if (request is null)
        {
            return "请求内容不能为空。";
        }

        if (!Guid.TryParse(request.RequestId, out requestId) || requestId == Guid.Empty)
        {
            return "requestId 必须是非空 UUID。";
        }

        if (!Guid.TryParse(request.CharacterId, out characterId) || characterId == Guid.Empty)
        {
            return "characterId 必须是非空 UUID。";
        }

        if (request.Amount is <= 0 or > MaximumMoney)
        {
            return $"金币数量必须在 1 到 {MaximumMoney:N0} 之间。";
        }

        amount = request.Amount;
        return null;
    }

    private static bool IsAccountPlayer(Player player, string accountName) =>
        player.IsConnected
        && player.Account is { } account
        && string.Equals(account.LoginName, accountName, StringComparison.OrdinalIgnoreCase);

    private static bool CanGrantToPlayer(Player player, string accountName, Guid characterId) =>
        IsAccountPlayer(player, accountName)
        && player.SelectedCharacter is { } character
        && character.Id == characterId
        && player.Inventory is not null
        && player.PlayerState.CurrentState == PlayerState.EnteredWorld;

    private static IEnumerable<IGameContext> GetStartedContexts(IEnumerable<IGameServer> servers) =>
        servers
            .Where(server => server.ServerState == ServerState.Started)
            .OfType<IGameServerContextProvider>()
            .Select(provider => provider.Context);

    private static MobileGmItem CreateItemSummary(ItemDefinition definition) =>
        new(
            definition.Group,
            definition.Number,
            definition.Name.ToString() ?? string.Empty,
            definition.MaximumItemLevel,
            CanHaveSkill(definition),
            CanHaveLuck(definition),
            CanHaveAdditionalOption(definition),
            CountExcellentOptions(definition));

    private static bool CanHaveSkill(ItemDefinition definition) =>
        definition.ItemSlot is not null && definition.Skill is not null && definition.QualifiedCharacters.Any();

    private static IEnumerable<IncreasableItemOption> OptionsOfType(ItemDefinition definition, ItemOptionType optionType) =>
        definition.PossibleItemOptions
            .SelectMany(optionDefinition => optionDefinition.PossibleOptions)
            .Where(option => option.OptionType == optionType);

    private static bool CanHaveLuck(ItemDefinition definition) =>
        OptionsOfType(definition, ItemOptionTypes.Luck).Any();

    private static bool CanHaveAdditionalOption(ItemDefinition definition) =>
        OptionsOfType(definition, ItemOptionTypes.Option).Any();

    private static int CountExcellentOptions(ItemDefinition definition) =>
        OptionsOfType(definition, ItemOptionTypes.Excellent).Count();

    /// <summary>Checks that every requested bit names an available excellent option.</summary>
    /// <param name="mask">The requested option mask.</param>
    /// <param name="options">The definition's available excellent options.</param>
    /// <returns>Whether all requested options exist.</returns>
    internal static bool HasValidExcellentOptions(int mask, IEnumerable<IncreasableItemOption> options)
    {
        if (mask < 0)
        {
            return false;
        }

        var allowedMask = 0;
        foreach (var option in options)
        {
            // The request uses a nonnegative signed 32-bit mask. Invalid option
            // numbers must not wrap around C#'s masked shift count.
            if (option.Number is >= 1 and <= 31)
            {
                allowedMask |= 1 << (option.Number - 1);
            }
        }

        return (mask & ~allowedMask) == 0;
    }

    private static void AddOptionLink(Player player, Item item, IncreasableItemOption option, int level)
    {
        var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
        link.ItemOption = option;
        link.Level = level;
        item.ItemOptions.Add(link);
    }

    private static void ApplyItemOptions(
        Player player,
        Item item,
        ItemDefinition definition,
        MobileGmGrantRequest request,
        IReadOnlyList<IncreasableItemOption> excellentOptions)
    {
        if (request.HasLuck)
        {
            var luckOption = OptionsOfType(definition, ItemOptionTypes.Luck).FirstOrDefault();
            if (luckOption is not null)
            {
                AddOptionLink(player, item, luckOption, 0);
            }
        }

        if (request.AdditionalOptionLevel > 0)
        {
            var additionalOption = OptionsOfType(definition, ItemOptionTypes.Option).FirstOrDefault();
            if (additionalOption is not null)
            {
                AddOptionLink(player, item, additionalOption, request.AdditionalOptionLevel);
            }
        }

        foreach (var excellentOption in excellentOptions)
        {
            if (((1 << (excellentOption.Number - 1)) & request.ExcellentMask) > 0)
            {
                AddOptionLink(player, item, excellentOption, 0);
            }
        }

        // Excellent items always carry their skill when the definition supports one.
        if (request.ExcellentMask > 0 && definition.Skill is not null)
        {
            item.HasSkill = true;
        }
    }

    private static async ValueTask RollBackAsync(Player player, IEnumerable<Item> addedItems, Item? unattachedItem = null)
    {
        if (unattachedItem is not null)
        {
            player.PersistenceContext.Detach(unattachedItem);
        }

        foreach (var item in addedItems.Reverse())
        {
            await player.Inventory!.RemoveItemAsync(item).ConfigureAwait(false);
            player.PersistenceContext.Detach(item);
        }
    }

    private string GetAccountName() =>
        Environment.GetEnvironmentVariable(MobileGmAuthenticationDefaults.AccountNameEnvironmentVariable) ?? string.Empty;

    private IGameServer[] GetServers() =>
        this._services.GetService<IDictionary<int, IGameServer>>()?.Values.ToArray() ?? [];

    private async Task<MobileGmGrantResponse> GrantItemCoreAsync(MobileGmGrantRequest request, Guid characterId)
    {
        var accountName = this.GetAccountName();
        if (accountName.Length == 0)
        {
            return new MobileGmGrantResponse(false, "本地游戏账号尚未配置。");
        }

        var player = await this.FindPlayerAsync(accountName, characterId).ConfigureAwait(false);
        if (player is null)
        {
            return new MobileGmGrantResponse(false, "角色当前不在线。");
        }

        return await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanGrantToPlayer(player, accountName, characterId))
            {
                return new MobileGmGrantResponse(false, "角色当前状态无法接收物品，请进入游戏世界后重试。");
            }

            var definition = player.GameContext.Configuration.Items.FirstOrDefault(
                item => item.Group == request.Group && item.Number == request.Number);
            if (definition is null)
            {
                return new MobileGmGrantResponse(false, "未找到该物品。");
            }

            if (definition.IsQuestItem || definition.IsBoundToCharacter)
            {
                return new MobileGmGrantResponse(false, "任务物品或角色绑定物品不能通过手机 GM 发放。");
            }

            if (request.Level > definition.MaximumItemLevel)
            {
                return new MobileGmGrantResponse(false, $"该物品最高只能设置为 +{definition.MaximumItemLevel}。");
            }

            if (request.HasSkill && !CanHaveSkill(definition))
            {
                return new MobileGmGrantResponse(false, "该物品不能附带技能。");
            }

            if (request.HasLuck && !CanHaveLuck(definition))
            {
                return new MobileGmGrantResponse(false, "该物品不能附带幸运属性。");
            }

            if (request.AdditionalOptionLevel > 0 && !CanHaveAdditionalOption(definition))
            {
                return new MobileGmGrantResponse(false, "该物品不能追加属性。");
            }

            var excellentOptions = OptionsOfType(definition, ItemOptionTypes.Excellent).ToList();
            if (request.ExcellentMask > 0)
            {
                if (excellentOptions.Count == 0)
                {
                    return new MobileGmGrantResponse(false, "该物品没有卓越属性。");
                }

                if (!HasValidExcellentOptions(request.ExcellentMask, excellentOptions))
                {
                    return new MobileGmGrantResponse(false, "所选卓越属性超出该物品可用范围。");
                }
            }

            if (definition.StorageLimitPerCharacter > 0
                && player.Inventory!.Items.Count(item => item.Definition == definition) + request.Quantity > definition.StorageLimitPerCharacter)
            {
                return new MobileGmGrantResponse(false, $"该物品每个角色最多持有 {definition.StorageLimitPerCharacter} 件。");
            }

            var addedItems = new List<Item>(request.Quantity);
            try
            {
                for (var index = 0; index < request.Quantity; index++)
                {
                    var item = player.PersistenceContext.CreateNew<Item>();
                    item.Definition = definition;
                    item.Level = (byte)request.Level;
                    item.Durability = item.GetMaximumDurabilityOfOnePiece();
                    item.HasSkill = request.HasSkill;
                    ApplyItemOptions(player, item, definition, request, excellentOptions);
                    if (!await player.Inventory!.AddItemAsync(item).ConfigureAwait(false))
                    {
                        await RollBackAsync(player, addedItems, item).ConfigureAwait(false);
                        return new MobileGmGrantResponse(false, "背包空间不足，未发放任何物品。");
                    }

                    addedItems.Add(item);
                }

                if (!await player.SaveProgressAsync().ConfigureAwait(false))
                {
                    await RollBackAsync(player, addedItems).ConfigureAwait(false);
                    return new MobileGmGrantResponse(false, "保存角色进度失败，未发放任何物品。");
                }
            }
            catch (Exception exception)
            {
                this._logger.LogError(exception, "Mobile GM item grant failed before commit for character {CharacterId}.", characterId);
                await RollBackAsync(player, addedItems).ConfigureAwait(false);
                return new MobileGmGrantResponse(false, "发放失败，未发放任何物品。");
            }

            foreach (var item in addedItems)
            {
                try
                {
                    await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(plugIn => plugIn.ItemAppearAsync(item)).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    this._logger.LogWarning(
                        exception,
                        "The client could not be notified about mobile GM item {ItemGroup}:{ItemNumber} in slot {ItemSlot}.",
                        item.Definition?.Group,
                        item.Definition?.Number,
                        item.ItemSlot);
                }
            }

            return new MobileGmGrantResponse(true, $"已向 {player.SelectedCharacter!.Name} 发放 {request.Quantity} 件物品。");
        }).ConfigureAwait(false);
    }

    private async Task<MobileGmGrantResponse> GrantZenCoreAsync(Guid characterId, long amount)
    {
        var accountName = this.GetAccountName();
        if (accountName.Length == 0)
        {
            return new MobileGmGrantResponse(false, "本地游戏账号尚未配置。");
        }

        var player = await this.FindPlayerAsync(accountName, characterId).ConfigureAwait(false);
        if (player is null)
        {
            return new MobileGmGrantResponse(false, "角色当前不在线。");
        }

        return await player.RunPersistenceExclusiveAsync(async () =>
        {
            if (player.SelectedCharacter?.Inventory is null
                || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            {
                return new MobileGmGrantResponse(false, "角色当前状态无法接收金币，请进入游戏世界后重试。");
            }

            var currentMoney = player.Money;
            var target = Math.Min(MaximumMoney, currentMoney + amount);
            var added = target - currentMoney;
            if (added <= 0)
            {
                return new MobileGmGrantResponse(false, "角色金币已达上限，无法继续增加。");
            }

            try
            {
                player.Money = (int)target;
                if (!await player.SaveProgressAsync().ConfigureAwait(false))
                {
                    player.Money = currentMoney;
                    return new MobileGmGrantResponse(false, "保存角色进度失败，未发放金币。");
                }
            }
            catch (Exception exception)
            {
                this._logger.LogError(exception, "Mobile GM Zen grant failed before commit for character {CharacterId}.", characterId);
                try
                {
                    player.Money = currentMoney;
                }
                catch (Exception rollbackException)
                {
                    this._logger.LogWarning(rollbackException, "Could not roll back Zen for character {CharacterId}.", characterId);
                }

                return new MobileGmGrantResponse(false, "发放失败，未发放金币。");
            }

            return new MobileGmGrantResponse(
                true,
                $"已向 {player.SelectedCharacter!.Name} 发放 {added:N0} 金币，当前余额 {target:N0}。");
        }).ConfigureAwait(false);
    }

    private async ValueTask<Player?> FindPlayerAsync(string accountName, Guid characterId)
    {
        foreach (var context in GetStartedContexts(this.GetServers()))
        {
            var player = (await context.GetPlayersAsync().ConfigureAwait(false)).FirstOrDefault(
                candidate => IsAccountPlayer(candidate, accountName) && candidate.SelectedCharacter?.Id == characterId);
            if (player is not null)
            {
                return player;
            }
        }

        return null;
    }

    private sealed record GrantOperation(MobileGmGrantRequest Request, AsyncLazy<MobileGmGrantResponse> Result);

    private sealed record ZenOperation(MobileGmZenRequest Request, AsyncLazy<MobileGmGrantResponse> Result);
}
