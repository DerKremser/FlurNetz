using FlurNetz.Modules.Automation.Domain;

namespace FlurNetz.Modules.Automation.Tests;

public sealed class AutomationRuleDomainTests
{
    private static readonly DateTimeOffset CreatedAt =
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1230);

    [Fact]
    public void CreateStartsDisabledAndNormalizesText()
    {
        var rule = CreateEngagementRule(
            displayName: "  Message-Regel  ",
            description: "  Beschreibung  ");

        Assert.NotEqual(Guid.Empty, rule.AutomationRuleId.Value);
        Assert.Equal("Message-Regel", rule.DisplayName);
        Assert.Equal("Beschreibung", rule.Description);
        Assert.False(rule.IsEnabled);
        Assert.False(rule.IsArchived);
        Assert.Equal(CreatedAt, rule.CreatedAtUtc);
        Assert.Equal(CreatedAt, rule.UpdatedAtUtc);
    }

    [Fact]
    public void LifecycleIsIdempotentAndArchiveIsTerminal()
    {
        var rule = CreateEngagementRule();
        var changedAt = CreatedAt.AddMinutes(1);

        Assert.False(rule.Disable(changedAt));
        Assert.True(rule.Enable(changedAt));
        Assert.False(rule.Enable(changedAt.AddMinutes(1)));
        Assert.True(rule.Disable(changedAt.AddMinutes(2)));
        Assert.True(rule.Archive(changedAt.AddMinutes(3)));
        Assert.False(rule.Archive(changedAt.AddMinutes(4)));
        Assert.False(rule.Disable(changedAt.AddMinutes(5)));
        Assert.Throws<AutomationRuleArchivedException>(() => rule.Enable(changedAt.AddMinutes(6)));
    }

    [Fact]
    public void ReplaceOnlyWorksWhileDisabledAndNoOpKeepsUpdatedAt()
    {
        var rule = CreateEngagementRule();
        var originalUpdatedAt = rule.UpdatedAtUtc;

        Assert.False(rule.ReplaceConfiguration(
            rule.DisplayName,
            rule.Description,
            rule.TriggerType,
            rule.Conditions,
            rule.Actions,
            rule.SortOrder,
            CreatedAt.AddHours(1)));
        Assert.Equal(originalUpdatedAt, rule.UpdatedAtUtc);

        Assert.True(rule.ReplaceConfiguration(
            "Neue Regel",
            null,
            rule.TriggerType,
            rule.Conditions,
            rule.Actions,
            2,
            CreatedAt.AddHours(1)));
        Assert.Equal("Neue Regel", rule.DisplayName);
        Assert.Equal(2, rule.SortOrder);

        rule.Enable(CreatedAt.AddHours(2));
        Assert.Throws<InvalidOperationException>(() => rule.ReplaceConfiguration(
            "Gesperrt",
            null,
            rule.TriggerType,
            rule.Conditions,
            rule.Actions,
            2,
            CreatedAt.AddHours(3)));
    }

    [Fact]
    public void ConditionsUseAndSemanticsAndEmptyConditionsMatchAll()
    {
        var identity = Guid.NewGuid();
        var offer = Guid.NewGuid();
        var item = Guid.NewGuid();
        var rule = AutomationRule.Create(
            AutomationRuleId.New(),
            "Shop",
            null,
            AutomationTriggerTypes.ShopPurchaseCompleted,
            [
                AutomationCondition.Create(0, AutomationConditionTypes.CommunityIdentityEquals, communityIdentityId: identity),
                AutomationCondition.Create(1, AutomationConditionTypes.ShopOfferIdEquals, shopOfferId: offer),
                AutomationCondition.Create(2, AutomationConditionTypes.ShopItemDefinitionIdEquals, itemDefinitionId: item),
                AutomationCondition.Create(3, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 10),
                AutomationCondition.Create(4, AutomationConditionTypes.ShopPricePaidAtMost, amount: 20)
            ],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)],
            createdAtUtc: CreatedAt);
        var snapshot = new AutomationTriggerSnapshot(
            Guid.NewGuid(),
            AutomationTriggerTypes.ShopPurchaseCompleted,
            1,
            CreatedAt,
            identity,
            Guid.NewGuid(),
            offer,
            item,
            15,
            CreatedAt);

        Assert.True(rule.Matches(snapshot));
        Assert.False(rule.Matches(new AutomationTriggerSnapshot(
            snapshot.TriggerMessageId,
            snapshot.MessageType,
            snapshot.SchemaVersion,
            snapshot.OccurredAtUtc,
            snapshot.CommunityIdentityId,
            snapshot.ShopPurchaseId,
            snapshot.ShopOfferId,
            snapshot.ItemDefinitionId,
            21,
            snapshot.PurchasedAtUtc)));

        var matchAll = AutomationRule.Create(
            AutomationRuleId.New(), "Alle", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], createdAtUtc: CreatedAt);
        Assert.True(matchAll.Matches(new AutomationTriggerSnapshot(
            Guid.NewGuid(), AutomationTriggerTypes.EngagementMessageRecorded, 1, CreatedAt, identity)));
    }

    [Fact]
    public void TriggerCompatibilityAndConditionDuplicatesAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Engagement", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [AutomationCondition.Create(0, AutomationConditionTypes.ShopOfferIdEquals, shopOfferId: Guid.NewGuid())],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], createdAtUtc: CreatedAt));

        Assert.Throws<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Duplicate", null, AutomationTriggerTypes.ShopPurchaseCompleted,
            [
                AutomationCondition.Create(0, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 5),
                AutomationCondition.Create(1, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 6)
            ],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], createdAtUtc: CreatedAt));

        Assert.Throws<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Range", null, AutomationTriggerTypes.ShopPurchaseCompleted,
            [
                AutomationCondition.Create(0, AutomationConditionTypes.ShopPricePaidAtLeast, amount: 20),
                AutomationCondition.Create(1, AutomationConditionTypes.ShopPricePaidAtMost, amount: 10)
            ],
            [AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 1)], createdAtUtc: CreatedAt));
    }

    [Fact]
    public void TextUnicodeLimitsAndMalformedRehydrateAreRejected()
    {
        Assert.Equal(
            AutomationRule.MaxDisplayNameLength,
            AutomationRule.Create(
                AutomationRuleId.New(),
                string.Concat(Enumerable.Repeat("😀", AutomationRule.MaxDisplayNameLength)),
                null,
                AutomationTriggerTypes.EngagementMessageRecorded,
                [],
                [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")],
                createdAtUtc: CreatedAt).DisplayName.EnumerateRunes().Count());
        Assert.Throws<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(),
            new string('a', AutomationRule.MaxDisplayNameLength + 1),
            null,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")],
            createdAtUtc: CreatedAt));
        Assert.Throws<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Name\0", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")], createdAtUtc: CreatedAt));
        Assert.Throws<ArgumentException>(() => AutomationRule.Rehydrate(
            AutomationRuleId.New(), " Name", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [], [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Title")],
            0, false, false, CreatedAt, CreatedAt));
    }

    [Fact]
    public void ActionValidationRequiresPositiveEconomyAndContiguousPositions()
    {
        Assert.Throws<ArgumentException>(() => AutomationAction.Create(0, AutomationActionTypes.EconomyCredit, amount: 0));
        Assert.Throws<ArgumentException>(() => AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, amount: 1, title: "Title"));
        Assert.Throws<ArgumentException>(() => AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: " \u2003 "));
        Assert.Throws<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Positions", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(1, AutomationActionTypes.EconomyCredit, amount: 1)],
            createdAtUtc: CreatedAt));
        Assert.ThrowsAny<ArgumentException>(() => AutomationRule.Create(
            AutomationRuleId.New(), "Many", null, AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            Enumerable.Range(0, AutomationRule.MaximumActions + 1)
                .Select(index => AutomationAction.Create(index, AutomationActionTypes.EconomyCredit, amount: 1)),
            createdAtUtc: CreatedAt));
    }

    private static AutomationRule CreateEngagementRule(string displayName = "Message", string? description = null) =>
        AutomationRule.Create(
            AutomationRuleId.New(),
            displayName,
            description,
            AutomationTriggerTypes.EngagementMessageRecorded,
            [],
            [AutomationAction.Create(0, AutomationActionTypes.NotificationCreate, title: "Erledigt")],
            createdAtUtc: CreatedAt);
}
