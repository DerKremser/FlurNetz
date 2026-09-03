using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Audit;

public enum AdminRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum AdminAuditOutcome
{
    Succeeded = 0,
    Rejected = 1,
    Failed = 2,
    OutcomeUnknown = 3
}

public sealed record AdminAuditEntry(
    Guid Id,
    CommunityIdentityId ActorCommunityIdentityId,
    string ActorLoginNameSnapshot,
    string Action,
    string TargetType,
    string TargetId,
    string? TargetDisplaySnapshot,
    AdminRiskLevel RiskLevel,
    string? Reason,
    AdminAuditOutcome Result,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid? RequestId,
    string? FailureCode,
    IReadOnlyDictionary<string, string?> ChangeSummary,
    IReadOnlyDictionary<string, string?> Metadata);

public interface IAdminAuditStore
{
    Task AppendAsync(AdminAuditEntry entry, CancellationToken cancellationToken = default);

    Task AppendAsync(
        AdminAuditEntry entry,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAuditEntry>> ListAsync(
        int take = 50,
        Guid? targetIdentityId = null,
        CancellationToken cancellationToken = default);
}
