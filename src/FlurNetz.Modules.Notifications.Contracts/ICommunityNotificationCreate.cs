using System.Data.Common;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Notifications.Contracts;

/// <summary>
/// Schmale, caller-neutrale Fähigkeit zum Erzeugen einer Community-Notification.
/// </summary>
/// <remarks>
/// Der Contract veröffentlicht weder Notifications-Domainobjekte noch Stores und kennt
/// keine SQL- oder Implementierungsdetails. Die bereitgestellte Transaktion bleibt im Besitz
/// des aufrufenden Messaging-Consumers.
/// </remarks>
public interface ICommunityNotificationCreate
{
    /// <summary>Erzeugt eine Notification innerhalb der bereitgestellten Transaktion.</summary>
    Task CreateAsync(
        CommunityIdentityId communityIdentityId,
        string notificationType,
        string title,
        string? message,
        string sourceType,
        string sourceId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
