// <copyright file="AccountService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Services;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Components;
using MUnique.OpenMU.Web.Shared.Components.Form.Modal;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Properties;

/// <summary>
/// Service for <see cref="Account"/>s.
/// </summary>
public class AccountService : IDataService<Account>, ISupportDataChangedNotification, IDisposable
{
    private readonly IDataSource<Account> _dataSource;
    private readonly IModalService _modalService;
    private readonly ILogger<AccountService> _logger;
    private readonly Debouncer _debouncer = new(300);

    private string _searchFilter = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountService"/> class.
    /// </summary>
    /// <param name="dataSource">The player context.</param>
    /// <param name="modalService">The modal service.</param>
    /// <param name="logger">The diagnostic logger.</param>
    public AccountService(IDataSource<Account> dataSource, IModalService modalService, ILogger<AccountService> logger)
    {
        this._dataSource = dataSource;
        this._modalService = modalService;
        this._logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? DataChanged;

    /// <summary>
    /// Gets or sets the search filter query.
    /// </summary>
    public string SearchFilter
    {
        get => this._searchFilter;
        set
        {
            var newValue = value ?? string.Empty;
            if (this._searchFilter != newValue)
            {
                this._searchFilter = newValue;
                if (string.IsNullOrEmpty(newValue))
                {
                    this._debouncer.Cancel();
                    this.DataChanged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _ = this._debouncer.DebounceAsync(this.RaiseDataChangedAsync);
                }
            }
        }
    }

    /// <summary>
    /// Returns a slice of the account list, defined by an offset and a count.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <returns>A slice of the account list, defined by an offset and a count.</returns>
    public async Task<List<Account>> GetAsync(int offset, int count)
    {
        try
        {
            var playerContext = (IPlayerContext)await this._dataSource.GetContextAsync().ConfigureAwait(false);
            var filter = this.SearchFilter.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                return (await playerContext.GetAccountsOrderedByLoginNameAsync(offset, count).ConfigureAwait(false)).ToList();
            }

            var results = (await playerContext.SearchAccountsAsync(filter, offset, count).ConfigureAwait(false)).ToList();
            if (results.Count == 0 && offset > 0)
            {
                // The filter narrowed the result set down to less entries than the current page offset - show the first page instead.
                results = (await playerContext.SearchAccountsAsync(filter, 0, count).ConfigureAwait(false)).ToList();
            }

            return results;
        }
        catch
        {
            return new List<Account>();
        }
    }

    /// <summary>
    /// Bans the specified account.
    /// </summary>
    /// <param name="account">The account.</param>
    public async ValueTask BanAsync(Account account)
    {
        account.State = AccountState.Banned;
        var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Unbans the specified account.
    /// </summary>
    /// <param name="account">The account.</param>
    public async ValueTask UnbanAsync(Account account)
    {
        account.State = AccountState.Normal;
        var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a new password hash for the specified account. Used by administrators
    /// when a player has lost their password and recovery credential.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="newPassword">The new plaintext password (already validated by the caller).</param>
    /// <returns><c>null</c> on success, an error message otherwise.</returns>
    public async ValueTask<string?> ResetPasswordAsync(Account account, string newPassword)
    {
        var context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        if (context is IAccountCredentialContext credentials)
        {
            var snapshot = await credentials.ReadCredentialsAsync(account.LoginName).ConfigureAwait(false);
            return snapshot is not null
                && await credentials.TryReplaceCredentialsAsync(snapshot, newHash, null).ConfigureAwait(false)
                ? null
                : "The password could not be saved. Please retry.";
        }

        account.PasswordHash = newHash;
        account.RecoveryCodeHash = null;
        return await context.SaveChangesAsync().ConfigureAwait(false)
            ? null
            : "The password could not be saved. Please retry.";
    }

    /// <summary>
    /// Creates a new Account in a modal dialog.
    /// </summary>
    public async Task CreateNewInModalDialogAsync()
    {
        var accountParameters = new AccountCreationParameters();
        var parameters = new ModalParameters();
        parameters.Add(nameof(ModalCreateNew<AccountCreationParameters>.Item), accountParameters);
        parameters.Add(
            nameof(ModalCreateNew<AccountCreationParameters>.CreateAsync),
            new Func<AccountCreationParameters, Task<string?>>(this.TryCreateAccountAsync));
        var options = new ModalOptions
        {
            DisableBackgroundCancel = true,
            HideCloseButton = true,
        };

        var modal = this._modalService.Show<ModalCreateNew<AccountCreationParameters>>($"Create {nameof(Account)}", parameters, options);
        var result = await modal.Result.ConfigureAwait(false);
        if (!result.Cancelled)
        {
            this.DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._debouncer.Dispose();
    }

    private async Task<string?> TryCreateAccountAsync(AccountCreationParameters parameters)
    {
        IContext? context = null;
        Account? account = null;
        try
        {
            context = await this._dataSource.GetContextAsync().ConfigureAwait(false);
            if (context is IPlayerContext playerContext
                && await playerContext.GetAccountByLoginNameAsync(parameters.LoginName).ConfigureAwait(false) is not null)
            {
                return "This login name is already in use. Choose a different name.";
            }

            if (context.HasChanges && !await context.SaveChangesAsync().ConfigureAwait(false))
            {
                return "Existing changes could not be saved. Please retry before creating an account.";
            }

            account = context.CreateNew<Account>();
            account.LoginName = parameters.LoginName;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(parameters.Password);
            account.EMail = parameters.EMail;
            account.State = parameters.State;
            account.SecurityCode = GameLogic.SecurityCodeSecurity.HashCode(parameters.SecurityCode);
            account.RegistrationDate = DateTime.UtcNow;
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("The account context did not confirm the save.");
            }

            return null;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Account creation failed.");
            if (account is not null && context is not null)
            {
                try
                {
                    context.Detach(account);
                }
                catch (Exception cleanupError)
                {
                    this._logger.LogError(cleanupError, "Could not detach the unsaved account.");
                    return "The account could not be saved. Reload this page before retrying.";
                }
            }

            return "The account could not be saved. Check the server log and database connection, then retry.";
        }
    }

    private Task RaiseDataChangedAsync()
    {
        this.DataChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Parameters for the account creation which is used for the user interface.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local", Justification = "Used by data binding.")]
    private class AccountCreationParameters
    {
        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_LoginName_Name))]
        [MaxLength(10)]
        [MinLength(3)]
        [RegularExpression("^[A-Za-z0-9]+$")]
        [Required]
        public string LoginName { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_Password_Name))]
        [MaxLength(20)]
        [MinLength(3)]
        [RegularExpression("^[\\x21-\\x7E]+$")]
        [Required]
        public string Password { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_SecurityCode_Name))]
        [MaxLength(10)]
        [MinLength(3)]
        [RegularExpression("^[\\x21-\\x7E]+$")]
        [Required]
        public string SecurityCode { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_EMail_Name))]
        public string EMail { get; set; } = string.Empty;

        [Display(ResourceType = typeof(Resources), Name = nameof(Resources.AccountCreationParameters_State_Name))]
        public AccountState State { get; set; }
    }
}
