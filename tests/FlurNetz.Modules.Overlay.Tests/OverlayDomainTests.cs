using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;
using System.Text;

namespace FlurNetz.Modules.Overlay.Tests;

/// <summary>Prüft die zentralen Overlay-Domaininvarianten.</summary>
public sealed class OverlayDomainTests
{
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero).AddTicks(1_230);

    [Fact]
    public void IdsRejectEmptyGuids()
    {
        Assert.Throws<ArgumentException>(() => OverlayChannelId.Create(Guid.Empty));
        Assert.Throws<ArgumentException>(() => OverlayAlertId.Create(Guid.Empty));
    }

    [Fact]
    public void ChannelUsesUnicodeScalarLimitsAndCanonicalText()
    {
        var id = OverlayChannelId.New();
        var channel = OverlayChannel.Create(id, string.Concat(Enumerable.Repeat("😀", 100)), null, Created);
        Assert.Equal(100, channel.DisplayName.EnumerateRunes().Count());
        Assert.Throws<ArgumentException>(() => OverlayChannel.Create(id, string.Concat(Enumerable.Repeat("😀", 101)), null, Created));
        Assert.Throws<ArgumentException>(() => OverlayChannel.Create(id, "bad\0value", null, Created));
        Assert.Throws<ArgumentException>(() => OverlayChannel.Create(id, "\uD800", null, Created));
        Assert.Equal("Name", OverlayChannel.Create(id, "  Name  ", null, Created).DisplayName);
    }

    [Fact]
    public void ChannelLifecycleIsIdempotentAndArchiveIsTerminal()
    {
        var channel = OverlayChannel.Create(OverlayChannelId.New(), "Alerts", null, Created);
        Assert.False(channel.Disable(Created.AddSeconds(1)));
        Assert.Equal(Created, channel.UpdatedAtUtc);
        Assert.True(channel.Enable(Created.AddSeconds(1)));
        Assert.False(channel.Enable(Created.AddSeconds(2)));
        Assert.Equal(Created.AddSeconds(1), channel.UpdatedAtUtc);
        Assert.True(channel.Archive(Created.AddSeconds(3)));
        Assert.False(channel.Archive(Created.AddSeconds(4)));
        Assert.False(channel.IsEnabled);
        Assert.Throws<OverlayChannelArchivedException>(() => channel.Enable(Created.AddSeconds(5)));
        Assert.Throws<OverlayChannelArchivedException>(() => channel.Rename("New", Created.AddSeconds(5)));
    }

    [Fact]
    public void RehydrateRejectsBrokenChannelStateAndTimestamp()
    {
        var id = OverlayChannelId.New();
        Assert.Throws<ArgumentException>(() => OverlayChannel.Rehydrate(id, "Name", null, true, true, Created, Created));
        Assert.Throws<ArgumentException>(() => OverlayChannel.Rehydrate(id, " Name", null, false, false, Created, Created));
        Assert.Throws<ArgumentException>(() => OverlayChannel.Rehydrate(id, "Name", null, false, false, Created, Created.AddTicks(1)));
        Assert.Throws<ArgumentException>(() => OverlayChannel.Rehydrate(id, "Name", null, false, false, Created.AddHours(1), Created));
    }

    [Fact]
    public void AlertsValidateVariantsDurationSourceAndExpiry()
    {
        var channel = OverlayChannelId.New();
        var alert = OverlayAlert.Create(OverlayAlertId.New(), channel, "Title", "Message", OverlayAlertVariant.Success, OverlayAlertDurationRules.DefaultMilliseconds, OverlaySourceReference.Create("shop", "42"), Created);
        Assert.Equal(Created.AddSeconds(5), alert.ExpiresAtUtc);
        Assert.Equal("shop", alert.SourceReference!.SourceType);
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayAlert.Create(OverlayAlertId.New(), channel, "Title", null, OverlayAlertVariant.Default, 999, null, Created));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayAlert.Create(OverlayAlertId.New(), channel, "Title", null, OverlayAlertVariant.Default, 30_001, null, Created));
        Assert.Throws<ArgumentException>(() => OverlayAlert.Create(OverlayAlertId.New(), channel, "Title", null, "custom", 5_000, null, Created));
        Assert.Throws<ArgumentException>(() => OverlaySourceReference.Create("type", " "));
        Assert.Throws<ArgumentException>(() => OverlayAlert.Rehydrate(OverlayAlertId.New(), channel, "Title", null, OverlayAlertVariant.Default, 5_000, null, Created, Created.AddSeconds(4)));
    }
}
