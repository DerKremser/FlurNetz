using FlurNetz.Modules.Administration.Domain;
using Microsoft.Extensions.Localization;

namespace FlurNetz.Api.Administration;

/// <summary>Lokalisierte, menschenlesbare Darstellung technischer Audit-Werte.</summary>
public static class AdminAuditPresentation
{
    private static readonly IReadOnlyDictionary<string, string> ActionResourceKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AdminAuditActions.CredentialChanged] = "AuditAction_CredentialChanged",
            [AdminAuditActions.CredentialRecovered] = "AuditAction_CredentialRecovered",
            [AdminAuditActions.PreferredCultureChanged] = "AuditAction_PreferredCultureChanged",
            [AdminAuditActions.BalanceCredited] = "AuditAction_BalanceCredited",
            [AdminAuditActions.BalanceDebited] = "AuditAction_BalanceDebited",
            [AdminAuditActions.ExperienceGranted] = "AuditAction_ExperienceGranted",
            [AdminAuditActions.QuantityAdded] = "AuditAction_QuantityAdded",
            [AdminAuditActions.QuantityRemoved] = "AuditAction_QuantityRemoved",
            [AdminAuditActions.DefinitionCreated] = "AuditAction_AchievementDefinitionCreated",
            [AdminAuditActions.DefinitionUpdated] = "AuditAction_AchievementDefinitionUpdated",
            [AdminAuditActions.AchievementUnlocked] = "AuditAction_AchievementUnlocked",
            [AdminAuditActions.TitleDefinitionCreated] = "AuditAction_TitleDefinitionCreated",
            [AdminAuditActions.TitleDefinitionUpdated] = "AuditAction_TitleDefinitionUpdated",
            [AdminAuditActions.TitleUnlocked] = "AuditAction_TitleUnlocked",
            [AdminAuditActions.TitleLocked] = "AuditAction_TitleLocked",
            [AdminAuditActions.RewardDefinitionCreated] = "AuditAction_RewardDefinitionCreated",
            [AdminAuditActions.RewardPackageCreated] = "AuditAction_RewardPackageCreated",
            [AdminAuditActions.RewardPackageGranted] = "AuditAction_RewardPackageGranted",
            [AdminAuditActions.OfferUpdated] = "AuditAction_OfferUpdated",
            [AdminAuditActions.OfferEnabled] = "AuditAction_OfferEnabled",
            [AdminAuditActions.OfferDisabled] = "AuditAction_OfferDisabled",
            [AdminAuditActions.OfferArchived] = "AuditAction_OfferArchived",
            [AdminAuditActions.RuleCreated] = "AuditAction_RuleCreated",
            [AdminAuditActions.RuleUpdated] = "AuditAction_RuleUpdated",
            [AdminAuditActions.RuleEnabled] = "AuditAction_RuleEnabled",
            [AdminAuditActions.RuleDisabled] = "AuditAction_RuleDisabled",
            [AdminAuditActions.RuleArchived] = "AuditAction_RuleArchived",
            [AdminAuditActions.ExternalIdentityLinked] = "AuditAction_ExternalIdentityLinked",
            [AdminAuditActions.ExternalIdentityUnlinked] = "AuditAction_ExternalIdentityUnlinked",
            [AdminAuditActions.ChannelCreated] = "AuditAction_ChannelCreated",
            [AdminAuditActions.ChannelUpdated] = "AuditAction_ChannelUpdated",
            [AdminAuditActions.ChannelEnabled] = "AuditAction_ChannelEnabled",
            [AdminAuditActions.ChannelDisabled] = "AuditAction_ChannelDisabled",
            [AdminAuditActions.ChannelArchived] = "AuditAction_ChannelArchived",
            [AdminAuditActions.SourceKeyRotated] = "AuditAction_SourceKeyRotated",
            [AdminAuditActions.PreviewPublished] = "AuditAction_PreviewPublished"
        };

    private static readonly IReadOnlyDictionary<string, string> TargetTypeResourceKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AdminCredential"] = "AuditTarget_AdminCredential",
            ["CommunityIdentity"] = "AuditTarget_CommunityIdentity",
            ["AchievementDefinition"] = "AuditTarget_AchievementDefinition",
            ["TitleDefinition"] = "AuditTarget_TitleDefinition",
            ["RewardDefinition"] = "AuditTarget_RewardDefinition",
            ["RewardPackage"] = "AuditTarget_RewardPackage",
            ["AutomationRule"] = "AuditTarget_AutomationRule",
            ["OverlayChannel"] = "AuditTarget_OverlayChannel",
            ["ShopOffer"] = "AuditTarget_ShopOffer",
            ["ExternalIdentityMapping"] = "AuditTarget_ExternalIdentityMapping"
        };

    public static string Action(IStringLocalizer<SharedResource> localizer, string? technicalValue) =>
        Localize(localizer, technicalValue, ActionResourceKeys);

    public static string TargetType(IStringLocalizer<SharedResource> localizer, string? technicalValue) =>
        Localize(localizer, technicalValue, TargetTypeResourceKeys);

    private static string Localize(
        IStringLocalizer<SharedResource> localizer,
        string? technicalValue,
        IReadOnlyDictionary<string, string> resourceKeys)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        if (string.IsNullOrWhiteSpace(technicalValue))
        {
            return string.Empty;
        }

        return resourceKeys.TryGetValue(technicalValue, out var resourceKey)
            ? localizer[resourceKey].Value
            : technicalValue;
    }
}
