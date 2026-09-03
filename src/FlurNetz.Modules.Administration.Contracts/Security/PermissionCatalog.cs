namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Stabiler, nicht lokalisierter Permission-Katalog der Administration V1.</summary>
public static class PermissionCatalog
{
    public const string Access = "Administration.Access";
    public const string DashboardRead = "Administration.Dashboard.Read";
    public const string IdentityRead = "Identity.Read";
    public const string EconomyRead = "Economy.Read";
    public const string EconomyAdjust = "Economy.Adjust";
    public const string ProgressionRead = "Progression.Read";
    public const string ProgressionGrantExperience = "Progression.GrantExperience";
    public const string InventoryRead = "Inventory.Read";
    public const string InventoryAdjust = "Inventory.Adjust";
    public const string ShopRead = "Shop.Read";
    public const string ShopManage = "Shop.Manage";
    public const string AchievementsRead = "Achievements.Read";
    public const string AchievementsManageDefinitions = "Achievements.ManageDefinitions";
    public const string AchievementsUnlock = "Achievements.Unlock";
    public const string TitlesRead = "Titles.Read";
    public const string TitlesManageDefinitions = "Titles.ManageDefinitions";
    public const string TitlesUnlock = "Titles.Unlock";
    public const string TitlesLock = "Titles.Lock";
    public const string RewardsRead = "Rewards.Read";
    public const string RewardsCreate = "Rewards.Create";
    public const string RewardsGrant = "Rewards.Grant";
    public const string NotificationsRead = "Notifications.Read";
    public const string AutomationRead = "Automation.Read";
    public const string AutomationManage = "Automation.Manage";
    public const string IntegrationsRead = "Integrations.Read";
    public const string IntegrationsManageMappings = "Integrations.ManageMappings";
    public const string OverlayRead = "Overlay.Read";
    public const string OverlayManage = "Overlay.Manage";
    public const string OverlayRotateSourceKey = "Overlay.RotateSourceKey";
    public const string AuditRead = "Audit.Read";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            Access, DashboardRead, IdentityRead, EconomyRead, EconomyAdjust,
            ProgressionRead, ProgressionGrantExperience, InventoryRead, InventoryAdjust,
            ShopRead, ShopManage, AchievementsRead, AchievementsManageDefinitions,
            AchievementsUnlock, TitlesRead, TitlesManageDefinitions, TitlesUnlock, TitlesLock,
            RewardsRead, RewardsCreate, RewardsGrant, NotificationsRead, AutomationRead,
            AutomationManage, IntegrationsRead, IntegrationsManageMappings, OverlayRead,
            OverlayManage, OverlayRotateSourceKey, AuditRead
        ],
        StringComparer.Ordinal);
}
