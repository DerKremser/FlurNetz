using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Das ausschließlich im Arbeitsspeicher vorgehaltene Gate für den First-Run-Setup.</summary>
public sealed record AdminSetupGateConfiguration(string? RequiredSecret)
{
    public bool IsConfigured => !string.IsNullOrEmpty(RequiredSecret);
}

public interface IAdminFirstRunSetup
{
    Task<AdminCredentialSnapshot> CreateFirstAdministratorAsync(
        string? email,
        string? password,
        string? passwordConfirmation,
        string? setupSecret,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminSetupClosedException() : InvalidOperationException("Das First-Run-Setup ist bereits abgeschlossen.");

public sealed class AdminSetupGateException() : InvalidOperationException("Das Setup-Gate ist ungültig.");

public interface IAdminCredentialRecovery
{
    Task<bool> RecoverAsync(
        CommunityIdentityId communityIdentityId,
        string newPassword,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
