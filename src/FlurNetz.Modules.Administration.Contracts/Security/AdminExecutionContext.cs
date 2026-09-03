using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>ASP.NET-unabhängiger Kontext eines authentifizierten Adminvorgangs.</summary>
public sealed record AdminExecutionContext(
    CommunityIdentityId ActorCommunityIdentityId,
    string ActorLoginName,
    string CorrelationId,
    IReadOnlySet<string> Permissions,
    Guid? RequestId = null);

public interface IAdminExecutionContextAccessor
{
    AdminExecutionContext? Current { get; set; }
}
