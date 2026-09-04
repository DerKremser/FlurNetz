namespace FlurNetz.Api.IntegrationTests;

public sealed class AdminShellUiTests
{
    [Fact]
    public void ShellExposesAccessibleNavigationAndLandmarks()
    {
        var layout = ReadUiSource("_AdminLayout.cshtml");

        Assert.Contains("href=\"#main-content\">Zum Hauptinhalt springen", layout, StringComparison.Ordinal);
        Assert.Contains("<nav class=\"nav-groups\" aria-label=\"Hauptnavigation\">", layout, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\" class=\"main-content\" tabindex=\"-1\">", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-toggle", layout, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"admin-navigation-drawer\"", layout, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-close", layout, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Navigation schließen\"", layout, StringComparison.Ordinal);
        Assert.Contains("/admin/admin.js", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("tabindex=\"1\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogNavigationHasOneServerActiveEntryAndARealRewardsTarget()
    {
        var layout = ReadUiSource("_AdminLayout.cshtml");
        var catalog = ReadUiSource("Catalog.cshtml");

        Assert.Contains("data-nav-key=\"catalog\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-fragment=\"rewards\"", layout, StringComparison.Ordinal);
        Assert.Contains("class=\"nav-link\" data-admin-nav-link data-nav-key=\"rewards\"", layout, StringComparison.Ordinal);
        Assert.Contains("id=\"rewards\"", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void NarrowNavigationScriptSupportsEscapeInertContentAndFocusReturn()
    {
        var script = ReadUiSource("admin.js");

        Assert.Contains("event.key === 'Escape'", script, StringComparison.Ordinal);
        Assert.Contains("navMain.inert = open", script, StringComparison.Ordinal);
        Assert.Contains("navToggle.setAttribute('aria-expanded'", script, StringComparison.Ordinal);
        Assert.Contains("navDrawer.setAttribute('aria-hidden'", script, StringComparison.Ordinal);
        Assert.Contains("focusTarget.focus()", script, StringComparison.Ordinal);
        Assert.Contains("setNavigationState(false, false)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedIdentitySearchIsExplicitlyDisabled()
    {
        var page = ReadUiSource("IdentitiesIndex.cshtml");

        Assert.Contains("class=\"search-box search-box-disabled\"", page, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Suche derzeit nicht verfügbar\"", page, StringComparison.Ordinal);
        Assert.Contains("disabled", page, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"identity-search-note\"", page, StringComparison.Ordinal);
        Assert.Contains("identity-search-note", page, StringComparison.Ordinal);
    }

    private static string ReadUiSource(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "UiSource", fileName));
}
