using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Administration.Domain;

public sealed class AdminBootstrapConflictException(string message) : InvalidOperationException(message);

public class AdminOperationConflictException(Guid requestId)
    : InvalidOperationException($"Die RequestId '{requestId}' wurde bereits mit anderen Requestdaten verwendet.");

public sealed class AdminOperationInProgressException(Guid requestId)
    : AdminOperationConflictException(requestId);

public sealed class AdminOperationAlreadyCompletedException(Guid requestId)
    : InvalidOperationException($"Die RequestId '{requestId}' wurde bereits erfolgreich verarbeitet.");

public sealed class AdminCredentialNotFoundException(CommunityIdentityId identityId)
    : KeyNotFoundException($"Für die Community-Identity '{identityId.Value}' existiert kein Admin-Credential.");
