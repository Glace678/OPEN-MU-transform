// <copyright file="SoloAccountInitializer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>Provisions an ordinary local account without changing any existing account or character.</summary>
public static class SoloAccountInitializer
{
    /// <summary>Creates the account once, or verifies that the existing account belongs to this installation.</summary>
    public static async Task EnsureAsync(IPlayerContext context, string username, string password)
    {
        if (username.Length is < 3 or > 10 || password.Length is < 12 or > 20
            || !username.All(char.IsAsciiLetterOrDigit)
            || !password.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new ArgumentException("Invalid local game credentials.");
        }

        var existing = await context.GetAccountByLoginNameAsync(username).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!BCrypt.Net.BCrypt.Verify(password, existing.PasswordHash))
            {
                throw new InvalidOperationException("The local game account already exists with different credentials. No account was changed.");
            }

            return;
        }

        var account = context.CreateNew<Account>();
        account.LoginName = username;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        account.State = AccountState.Normal;
        account.LanguageIsoCode = "zh";
        account.RegistrationDate = DateTime.UtcNow;
        if (!await context.SaveChangesAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("The local game account could not be saved.");
        }
    }
}
