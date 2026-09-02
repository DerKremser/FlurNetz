using System.Globalization;
using FlurNetz.Api.Contracts;
using FlurNetz.Api.Cursors;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlurNetz.Api.Endpoints;

/// <summary>
/// Ordnet die persönliche Notifications-Inbox der HTTP-Grenze zu.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/identities/{communityIdentityId}/notifications",
            ListNotificationsAsync);
        endpoints.MapGet(
            "/api/identities/{communityIdentityId}/notifications/unread-count",
            GetUnreadCountAsync);
        endpoints.MapGet(
            "/api/identities/{communityIdentityId}/notifications/{notificationId}",
            GetNotificationAsync);
        endpoints.MapPost(
            "/api/identities/{communityIdentityId}/notifications/{notificationId}/read",
            MarkReadAsync);
        endpoints.MapPost(
            "/api/identities/{communityIdentityId}/notifications/{notificationId}/unread",
            MarkUnreadAsync);
        endpoints.MapPost(
            "/api/identities/{communityIdentityId}/notifications/read-all",
            MarkAllReadAsync);

        return endpoints;
    }

    private static async Task<IResult> ListNotificationsAsync(
        string communityIdentityId,
        string? pageSize,
        string? cursor,
        string? unreadOnly,
        ListNotificationsForIdentity useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität ist ungültig.");
        }

        if (!TryParsePageSize(pageSize, out var validPageSize))
        {
            return InvalidRequest("Die Seitengröße muss zwischen 1 und 100 liegen.");
        }

        if (!TryParseBool(unreadOnly, out var validUnreadOnly))
        {
            return InvalidRequest("Der unreadOnly-Filter ist ungültig.");
        }

        NotificationInboxCursor? inboxCursor = null;
        if (cursor is not null
            && !NotificationInboxCursorCodec.TryDecode(
                cursor,
                identityId,
                validUnreadOnly,
                out inboxCursor))
        {
            return InvalidRequest("Der Notification-Cursor ist ungültig.");
        }

        var page = await useCase.ExecuteAsync(
                identityId,
                inboxCursor,
                validUnreadOnly,
                validPageSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new CommunityNotificationListResponse(
            page.Items.Select(ToResponse).ToArray(),
            page.NextCursor is null ? null : NotificationInboxCursorCodec.Encode(page.NextCursor)));
    }

    private static async Task<IResult> GetNotificationAsync(
        string communityIdentityId,
        string notificationId,
        GetNotification useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität ist ungültig.");
        }

        if (!TryCreateId(notificationId, NotificationId.Create, out var validNotificationId))
        {
            return InvalidRequest("Die Route-ID der Notification ist ungültig.");
        }

        var notification = await useCase
            .ExecuteAsync(identityId, validNotificationId, cancellationToken)
            .ConfigureAwait(false);

        return notification is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(notification));
    }

    private static async Task<IResult> GetUnreadCountAsync(
        string communityIdentityId,
        GetUnreadNotificationCount useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität ist ungültig.");
        }

        var unreadCount = await useCase.ExecuteAsync(identityId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new UnreadNotificationCountResponse(unreadCount));
    }

    private static async Task<IResult> MarkReadAsync(
        string communityIdentityId,
        string notificationId,
        MarkNotificationRead useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId)
            || !TryCreateId(notificationId, NotificationId.Create, out var validNotificationId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität oder Notification ist ungültig.");
        }

        return await useCase.ExecuteAsync(identityId, validNotificationId, cancellationToken)
                .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> MarkUnreadAsync(
        string communityIdentityId,
        string notificationId,
        MarkNotificationUnread useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId)
            || !TryCreateId(notificationId, NotificationId.Create, out var validNotificationId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität oder Notification ist ungültig.");
        }

        return await useCase.ExecuteAsync(identityId, validNotificationId, cancellationToken)
                .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> MarkAllReadAsync(
        string communityIdentityId,
        MarkAllNotificationsRead useCase,
        CancellationToken cancellationToken)
    {
        if (!TryCreateId(communityIdentityId, CommunityIdentityId.Create, out var identityId))
        {
            return InvalidRequest("Die Route-ID der Community-Identität ist ungültig.");
        }

        var markedCount = await useCase.ExecuteAsync(identityId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new MarkAllNotificationsReadResponse(markedCount));
    }

    private static CommunityNotificationResponse ToResponse(CommunityNotification notification) =>
        new(
            notification.Id.Value,
            notification.CommunityIdentityId.Value,
            notification.NotificationType,
            notification.Title,
            notification.Message,
            notification.SourceReference?.SourceType,
            notification.SourceReference?.SourceId,
            notification.CreatedAtUtc,
            notification.ReadAtUtc,
            notification.IsRead);

    private static bool TryParsePageSize(string? rawPageSize, out int pageSize)
    {
        if (rawPageSize is null)
        {
            pageSize = ListNotificationsForIdentity.DefaultPageSize;
            return true;
        }

        return int.TryParse(
                rawPageSize,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out pageSize)
            && pageSize is >= ListNotificationsForIdentity.MinimumPageSize
                and <= ListNotificationsForIdentity.MaximumPageSize;
    }

    private static bool TryParseBool(string? rawValue, out bool value)
    {
        if (rawValue is null)
        {
            value = false;
            return true;
        }

        return bool.TryParse(rawValue, out value);
    }

    private static bool TryCreateId<TId>(
        string rawId,
        Func<Guid, TId> create,
        out TId id)
    {
        id = default!;
        return Guid.TryParse(rawId, out var value)
            && value != Guid.Empty
            && TryCreate(value, create, out id);
    }

    private static bool TryCreate<TId>(Guid value, Func<Guid, TId> create, out TId id)
    {
        try
        {
            id = create(value);
            return true;
        }
        catch (ArgumentException)
        {
            id = default!;
            return false;
        }
    }

    private static IResult InvalidRequest(string detail) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Ungültige Anfrage.",
        detail: detail);
}
