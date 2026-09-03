using FlurNetz.Api.Contracts;
using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Titles.Application;

namespace FlurNetz.Api.Endpoints;

public static class AdminIdentityEndpoints
{
    private const string Route = "/api/admin/identities";

    public static IEndpointRouteBuilder MapAdminIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, ListAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IdentityRead));
        endpoints.MapGet($"{Route}/{{communityIdentityId}}", DetailAsync)
            .RequireAuthorization(AdminPolicies.ForPermission(PermissionCatalog.IdentityRead));
        return endpoints;
    }

    private static async Task<IResult> ListAsync(string? after, int? pageSize, ICommunityIdentityRead reader, CancellationToken token)
    {
        CommunityIdentityId? cursor = null;
        if (after is not null)
        {
            if (!Guid.TryParse(after, out var raw) || raw == Guid.Empty) return Invalid("Der Cursor ist ungültig.");
            cursor = CommunityIdentityId.Create(raw);
        }

        var take = pageSize ?? 25;
        if (take is < 1 or > 100) return Invalid("Die Seitengröße muss zwischen 1 und 100 liegen.");
        var page = await reader.ListAsync(cursor, take, token).ConfigureAwait(false);
        return Results.Ok(new AdminIdentityListResponse(
            page.Items.Select(item => item.CommunityIdentityId.Value).ToArray(),
            page.NextCursor?.Value.ToString("D")));
    }

    private static async Task<IResult> DetailAsync(
        string communityIdentityId,
        ICommunityIdentityRead identityReader,
        IExternalIdentityMappingStore mappingStore,
        ICommunityEconomyStore economyStore,
        ICommunityProgressionStore progressionStore,
        ICommunityInventoryStore inventoryStore,
        ListCommunityAchievements achievementsReader,
        ICommunityTitlesStore titlesStore,
        ListShopPurchasesForIdentity purchasesReader,
        ListNotificationsForIdentity notificationsReader,
        GetUnreadNotificationCount unreadReader,
        IAdminAuditStore auditStore,
        CancellationToken token)
    {
        if (!Guid.TryParse(communityIdentityId, out var raw) || raw == Guid.Empty) return Invalid("Die Identity-ID ist ungültig.");
        var identity = CommunityIdentityId.Create(raw);
        if (await identityReader.GetAsync(identity, token).ConfigureAwait(false) is null) return Results.NotFound();

        var mappings = await mappingStore.ListForCommunityIdentityAsync(identity, token).ConfigureAwait(false);
        var economy = await economyStore.GetByCommunityIdentityIdAsync(identity, token).ConfigureAwait(false);
        var progression = await progressionStore.GetByCommunityIdentityIdAsync(identity, token).ConfigureAwait(false);
        var inventory = await inventoryStore.ListAsync(identity, token).ConfigureAwait(false);
        var achievements = await achievementsReader.ExecuteAsync(identity, token).ConfigureAwait(false);
        var titles = await titlesStore.GetAsync(identity, token).ConfigureAwait(false);
        var purchases = await purchasesReader.ExecuteAsync(identity, pageSize: 25, cancellationToken: token).ConfigureAwait(false);
        var notifications = await notificationsReader.ExecuteAsync(identity, pageSize: 25, cancellationToken: token).ConfigureAwait(false);
        var unread = await unreadReader.ExecuteAsync(identity, token).ConfigureAwait(false);
        var audit = await auditStore.ListAsync(50, raw, token).ConfigureAwait(false);
        return Results.Ok(new AdminIdentityDetailResponse(
            raw,
            mappings.Select(mapping => new AdminExternalIdentityResponse(mapping.ProviderKey.Value, mapping.ExternalUserId.Value)).ToArray(),
            economy is null ? null : new AdminEconomyResponse(economy.Balance.Value),
            progression is null ? null : new AdminProgressionResponse(progression.ExperiencePoints.Value),
            inventory.Select(entry => new AdminInventoryEntryResponse(entry.ItemDefinitionId.Value, entry.Quantity.Value)).ToArray(),
            achievements.Select(achievement => new AdminAchievementResponse(achievement.AchievementDefinitionId.Value, achievement.UnlockedAtUtc)).ToArray(),
            titles is null ? null : new AdminTitlesResponse(identity.Value, titles.UnlockedTitleDefinitionIds.Select(id => id.Value).ToArray(), titles.CurrentTitleDefinitionId?.Value),
            purchases.Items.Select(purchase => new AdminShopPurchaseResponse(purchase.Id.Value, purchase.ShopOfferId.Value, purchase.ItemDefinitionId.Value, purchase.PricePaid.Value, purchase.PurchasedAtUtc)).ToArray(),
            notifications.Items.Select(notification => new AdminNotificationResponse(notification.Id.Value, notification.NotificationType, notification.Title, notification.Message, notification.IsRead, notification.CreatedAtUtc)).ToArray(),
            unread,
            audit.Select(entry => new AdminAuditResponse(entry.Action, entry.TargetType, entry.TargetId, entry.Result.ToString(), entry.RiskLevel.ToString(), entry.Reason, entry.OccurredAtUtc)).ToArray()));
    }

    private static IResult Invalid(string detail) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ungültige Anfrage.", detail: detail);
}
