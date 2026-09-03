using FlurNetz.Modules.Administration.Contracts.Audit;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Achievements.Application;
using FlurNetz.Modules.Achievements.Domain;
using FlurNetz.Modules.Economy.Application;
using FlurNetz.Modules.Economy.Domain;
using FlurNetz.Modules.Identity.Contracts;
using FlurNetz.Modules.Integrations.Application;
using FlurNetz.Modules.Integrations.Domain;
using FlurNetz.Modules.Inventory.Application;
using FlurNetz.Modules.Inventory.Domain;
using FlurNetz.Modules.Notifications.Application;
using FlurNetz.Modules.Notifications.Domain;
using FlurNetz.Modules.Progression.Application;
using FlurNetz.Modules.Progression.Domain;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Titles.Application;
using FlurNetz.Modules.Titles.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlurNetz.Api.Pages.Admin.Identities;

[Authorize(Policy = "Admin.Identity.Read")]
public sealed class DetailsModel(
    ICommunityIdentityRead identityRead,
    ICommunityEconomyStore economyStore,
    ICommunityProgressionStore progressionStore,
    ICommunityInventoryStore inventoryStore,
    ListCommunityAchievements achievementsReader,
    ICommunityTitlesStore titlesStore,
    ListShopPurchasesForIdentity purchasesReader,
    ListExternalIdentityMappings mappingsReader,
    ListNotificationsForIdentity notificationsReader,
    GetUnreadNotificationCount unreadReader,
    IAdminAuditStore auditStore) : PageModel
{
    public Guid IdentityId { get; private set; }
    public CommunityEconomy? Economy { get; private set; }
    public CommunityProgression? Progression { get; private set; }
    public IReadOnlyList<CommunityInventoryEntry> Inventory { get; private set; } = [];
    public IReadOnlyList<CommunityAchievement> Achievements { get; private set; } = [];
    public CommunityTitles? Titles { get; private set; }
    public IReadOnlyList<ShopPurchase> ShopPurchases { get; private set; } = [];
    public IReadOnlyList<ExternalIdentityMapping> Mappings { get; private set; } = [];
    public IReadOnlyList<CommunityNotification> Notifications { get; private set; } = [];
    public IReadOnlyList<AdminAuditEntry> AuditEntries { get; private set; } = [];
    public long? UnreadCount { get; private set; }
    public bool NotificationsUnavailable { get; private set; }
    public bool EconomyUnavailable { get; private set; }
    public bool ProgressionUnavailable { get; private set; }
    public bool InventoryUnavailable { get; private set; }
    public bool AchievementsUnavailable { get; private set; }
    public bool TitlesUnavailable { get; private set; }
    public bool ShopPurchasesUnavailable { get; private set; }
    public bool MappingsUnavailable { get; private set; }
    public bool AuditUnavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid communityIdentityId, CancellationToken cancellationToken)
    {
        if (communityIdentityId == Guid.Empty)
        {
            return NotFound();
        }

        var identity = CommunityIdentityId.Create(communityIdentityId);
        if (await identityRead.GetAsync(identity, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }

        IdentityId = communityIdentityId;
        var economyTask = ReadEconomyAsync(identity, cancellationToken);
        var progressionTask = ReadProgressionAsync(identity, cancellationToken);
        var inventoryTask = ReadInventoryAsync(identity, cancellationToken);
        var achievementsTask = ReadAchievementsAsync(identity, cancellationToken);
        var titlesTask = ReadTitlesAsync(identity, cancellationToken);
        var purchasesTask = ReadPurchasesAsync(identity, cancellationToken);
        var mappingsTask = ReadMappingsAsync(identity, cancellationToken);
        var notificationsTask = ReadNotificationsAsync(identity, cancellationToken);
        var unreadTask = ReadUnreadAsync(identity, cancellationToken);
        var auditTask = ReadAuditAsync(communityIdentityId, cancellationToken);
        await Task.WhenAll(economyTask, progressionTask, inventoryTask, achievementsTask, titlesTask, purchasesTask, mappingsTask, notificationsTask, unreadTask, auditTask).ConfigureAwait(false);
        Economy = economyTask.Result;
        Progression = progressionTask.Result;
        Inventory = inventoryTask.Result;
        Achievements = achievementsTask.Result;
        Titles = titlesTask.Result;
        ShopPurchases = purchasesTask.Result;
        Mappings = mappingsTask.Result;
        Notifications = notificationsTask.Result;
        UnreadCount = unreadTask.Result;
        AuditEntries = auditTask.Result;
        return Page();
    }

    private async Task<CommunityEconomy?> ReadEconomyAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await economyStore.GetByCommunityIdentityIdAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { EconomyUnavailable = true; return null; }
    }

    private async Task<CommunityProgression?> ReadProgressionAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await progressionStore.GetByCommunityIdentityIdAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { ProgressionUnavailable = true; return null; }
    }

    private async Task<IReadOnlyList<CommunityInventoryEntry>> ReadInventoryAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await inventoryStore.ListAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { InventoryUnavailable = true; return []; }
    }

    private async Task<IReadOnlyList<CommunityAchievement>> ReadAchievementsAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await achievementsReader.ExecuteAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { AchievementsUnavailable = true; return []; }
    }

    private async Task<CommunityTitles?> ReadTitlesAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await titlesStore.GetAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { TitlesUnavailable = true; return null; }
    }

    private async Task<IReadOnlyList<ShopPurchase>> ReadPurchasesAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return (await purchasesReader.ExecuteAsync(id, pageSize: 10, cancellationToken: token).ConfigureAwait(false)).Items; }
        catch (Exception exception) when (exception is not OperationCanceledException) { ShopPurchasesUnavailable = true; return []; }
    }

    private async Task<IReadOnlyList<ExternalIdentityMapping>> ReadMappingsAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await mappingsReader.ExecuteAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { MappingsUnavailable = true; return []; }
    }

    private async Task<IReadOnlyList<CommunityNotification>> ReadNotificationsAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return (await notificationsReader.ExecuteAsync(id, pageSize: 10, cancellationToken: token).ConfigureAwait(false)).Items; }
        catch (Exception exception) when (exception is not OperationCanceledException) { NotificationsUnavailable = true; return []; }
    }

    private async Task<long?> ReadUnreadAsync(CommunityIdentityId id, CancellationToken token)
    {
        try { return await unreadReader.ExecuteAsync(id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { NotificationsUnavailable = true; return null; }
    }

    private async Task<IReadOnlyList<AdminAuditEntry>> ReadAuditAsync(Guid id, CancellationToken token)
    {
        try { return await auditStore.ListAsync(25, id, token).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException) { AuditUnavailable = true; return []; }
    }
}
