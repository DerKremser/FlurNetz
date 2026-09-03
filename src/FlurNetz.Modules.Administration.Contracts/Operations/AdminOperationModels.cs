using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Operations;

public enum AdminMutationStatus
{
    Reserved = 0,
    Succeeded = 1,
    Rejected = 2,
    Failed = 3,
    OutcomeUnknown = 4
}

public enum AdminOperationAuditStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public sealed record AdminOperationReservation(
    Guid RequestId,
    CommunityIdentityId ActorCommunityIdentityId,
    string OperationType,
    string TargetType,
    string TargetId,
    string RequestFingerprint,
    string CorrelationId,
    AdminMutationStatus MutationStatus,
    AdminOperationAuditStatus AuditStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    /// <summary>True only for the caller that inserted the reservation in the current transaction.</summary>
    public bool IsNew { get; init; }
}

public sealed record AdminMutationCommand(
    Guid RequestId,
    CommunityIdentityId ActorCommunityIdentityId,
    string OperationType,
    string TargetType,
    string TargetId,
    string RequestFingerprint,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminMutationResult(
    bool AlreadyCompleted,
    AdminMutationStatus Status);

public interface IAdminOperationStore
{
    Task<AdminOperationReservation?> FindAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<AdminOperationReservation> ReserveAsync(
        AdminOperationReservation operation,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid requestId,
        AdminMutationStatus mutationStatus,
        AdminOperationAuditStatus auditStatus,
        DateTimeOffset completedAtUtc,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}

/// <summary>Erzeugt einen kanonischen Fingerprint ohne volatile Transportfelder.</summary>
public static class AdminRequestFingerprint
{
    public static string Compute(params (string Name, object? Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var canonical = string.Join(
            "\n",
            values
                .OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => $"{value.Name}={Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}"));
        return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
