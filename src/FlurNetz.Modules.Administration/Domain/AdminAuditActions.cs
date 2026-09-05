namespace FlurNetz.Modules.Administration.Domain;

/// <summary>Stabile technische Audit-Action-IDs.</summary>
public static class AdminAuditActions
{
    public const string CredentialChanged = "Administration.CredentialChanged";
    public const string CredentialRecovered = "Administration.CredentialRecovered";
    public const string PreferredCultureChanged = "Administration.PreferredCultureChanged";
    public const string BalanceCredited = "Economy.BalanceCredited";
    public const string BalanceDebited = "Economy.BalanceDebited";
    public const string ExperienceGranted = "Progression.ExperienceGranted";
    public const string QuantityAdded = "Inventory.QuantityAdded";
    public const string QuantityRemoved = "Inventory.QuantityRemoved";
    public const string DefinitionCreated = "Achievements.DefinitionCreated";
    public const string DefinitionUpdated = "Achievements.DefinitionUpdated";
    public const string AchievementUnlocked = "Achievements.Unlocked";
    public const string TitleDefinitionCreated = "Titles.DefinitionCreated";
    public const string TitleDefinitionUpdated = "Titles.DefinitionUpdated";
    public const string TitleUnlocked = "Titles.Unlocked";
    public const string TitleLocked = "Titles.Locked";
    public const string RewardDefinitionCreated = "Rewards.DefinitionCreated";
    public const string RewardPackageCreated = "Rewards.PackageCreated";
    public const string RewardPackageGranted = "Rewards.PackageGranted";
    public const string OfferUpdated = "Shop.OfferUpdated";
    public const string OfferEnabled = "Shop.OfferEnabled";
    public const string OfferDisabled = "Shop.OfferDisabled";
    public const string OfferArchived = "Shop.OfferArchived";
    public const string RuleCreated = "Automation.RuleCreated";
    public const string RuleUpdated = "Automation.RuleUpdated";
    public const string RuleEnabled = "Automation.RuleEnabled";
    public const string RuleDisabled = "Automation.RuleDisabled";
    public const string RuleArchived = "Automation.RuleArchived";
    public const string ExternalIdentityLinked = "Integrations.ExternalIdentityLinked";
    public const string ExternalIdentityUnlinked = "Integrations.ExternalIdentityUnlinked";
    public const string ChannelCreated = "Overlay.ChannelCreated";
    public const string ChannelUpdated = "Overlay.ChannelUpdated";
    public const string ChannelEnabled = "Overlay.ChannelEnabled";
    public const string ChannelDisabled = "Overlay.ChannelDisabled";
    public const string ChannelArchived = "Overlay.ChannelArchived";
    public const string SourceKeyRotated = "Overlay.SourceKeyRotated";
    public const string PreviewPublished = "Overlay.PreviewPublished";
}
