using System.Security.Cryptography;
using System.Text;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Administration.Application;

/// <summary>
/// Führt die einmalige, gate-geschützte Anlage des ersten Administrators aus.
/// </summary>
public sealed class AdminFirstRunSetup(
    IPostgreSqlConnectionFactory connectionFactory,
    ICommunityIdentityCreator identityCreator,
    IAdminCredentialStore credentialStore,
    IAdminPasswordHasher passwordHasher,
    AdminSetupGateConfiguration setupGate) : IAdminFirstRunSetup
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            var available = await credentialStore.IsFirstRunAvailableAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return available;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<AdminCredentialSnapshot> CreateFirstAdministratorAsync(
        string? email,
        string? password,
        string? passwordConfirmation,
        string? setupSecret,
        CancellationToken cancellationToken = default)
    {
        if (!setupGate.IsConfigured || !SecretsEqual(setupGate.RequiredSecret, setupSecret))
        {
            throw new AdminSetupGateException();
        }

        var canonicalEmail = AdminEmail.Canonicalize(email);
        AdminPasswordPolicy.Validate(password);
        if (!string.Equals(password, passwordConfirmation, StringComparison.Ordinal))
        {
            throw new ArgumentException("Die Passwortbestätigung stimmt nicht überein.", nameof(passwordConfirmation));
        }

        await using var transaction = await PostgreSqlTransaction.BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await credentialStore.IsFirstRunAvailableAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                throw new AdminSetupClosedException();
            }

            var identityId = await identityCreator.CreateAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var credential = AdminCredential.Create(
                identityId,
                canonicalEmail,
                passwordHasher.Hash(password!),
                now);
            await credentialStore.AddCredentialAsync(
                    credential,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await credentialStore.AddRoleAssignmentAsync(
                    identityId,
                    AdminRole.Administrator,
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await credentialStore.CompleteFirstRunSetupAsync(
                    transaction.Connection,
                    transaction.Transaction,
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return credential.ToSnapshot();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static bool SecretsEqual(string? expected, string? actual)
    {
        if (expected is null || actual is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}
