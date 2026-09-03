using System.Data.Common;
using FlurNetz.Modules.Administration.Domain;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Application;

public interface IAdminCredentialStore
{
    Task<AdminCredential?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<AdminCredential?> GetByIdentityAsync(CommunityIdentityId identityId, CancellationToken cancellationToken = default);
    Task<AdminCredential?> GetByEmailAsync(string normalizedEmail, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task<AdminCredential?> GetByIdentityAsync(CommunityIdentityId identityId, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task AddCredentialAsync(AdminCredential credential, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task AddRoleAssignmentAsync(CommunityIdentityId identityId, string roleName, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> IsFirstRunAvailableAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task CompleteFirstRunSetupAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(AdminCredential credential, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);
}
