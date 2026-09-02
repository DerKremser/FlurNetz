using FlurNetz.Modules.Automation.Domain;
using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Automation.Tests;

public sealed class AutomationOverlayActionTests
{
    [Fact]
    public void OverlayAlertUsesTheSharedContractShape()
    {
        var channelId = OverlayChannelId.New();

        var action = AutomationAction.Create(
            2,
            AutomationActionTypes.OverlayAlert,
            title: "  Stream gestartet  ",
            message: "  Willkommen  ",
            overlayChannelId: channelId,
            variant: OverlayAlertVariant.Success,
            durationMilliseconds: OverlayAlertDurationRules.DefaultMilliseconds);

        Assert.Equal(2, action.Position);
        Assert.Equal(AutomationActionTypes.OverlayAlert, action.ActionType);
        Assert.Equal(channelId, action.OverlayChannelId);
        Assert.Equal("Stream gestartet", action.Title);
        Assert.Equal("Willkommen", action.Message);
        Assert.Equal(OverlayAlertVariant.Success, action.Variant);
        Assert.Equal(OverlayAlertDurationRules.DefaultMilliseconds, action.DurationMilliseconds);
        Assert.Null(action.Amount);
    }

    [Fact]
    public void OverlayAlertRejectsForeignValueFieldsAndInvalidConfiguration()
    {
        var channelId = OverlayChannelId.New();

        Assert.Throws<ArgumentException>(() => AutomationAction.Create(
            0,
            AutomationActionTypes.OverlayAlert,
            amount: 1,
            title: "Alert",
            overlayChannelId: channelId,
            variant: OverlayAlertVariant.Default,
            durationMilliseconds: OverlayAlertDurationRules.DefaultMilliseconds));
        Assert.Throws<ArgumentException>(() => AutomationAction.Create(
            0,
            AutomationActionTypes.OverlayAlert,
            title: "Alert",
            overlayChannelId: channelId,
            variant: "custom",
            durationMilliseconds: OverlayAlertDurationRules.DefaultMilliseconds));
        Assert.Throws<ArgumentException>(() => AutomationAction.Create(
            0,
            AutomationActionTypes.OverlayAlert,
            title: "Alert",
            overlayChannelId: channelId,
            variant: OverlayAlertVariant.Default,
            durationMilliseconds: OverlayAlertDurationRules.MinimumMilliseconds - 1));
    }
}
