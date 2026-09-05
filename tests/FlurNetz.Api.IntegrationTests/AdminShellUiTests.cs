using System.Xml.Linq;

namespace FlurNetz.Api.IntegrationTests;

public sealed class AdminShellUiTests
{
    [Fact]
    public void ShellExposesAccessibleNavigationAndLandmarks()
    {
        var layout = ReadUiSource("_AdminLayout.cshtml");

        Assert.Contains("href=\"#main-content\">@L[\"Layout_SkipToContent\"]", layout, StringComparison.Ordinal);
        Assert.Contains("<nav class=\"nav-groups\" aria-label=\"@L[\"Layout_MainNavigation\"]\">", layout, StringComparison.Ordinal);
        Assert.Contains("<main id=\"main-content\" class=\"main-content\" tabindex=\"-1\">", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-toggle", layout, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"admin-navigation-drawer\"", layout, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-close", layout, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"@L[\"Nav_Close\"]\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-label-open=\"@L[\"Nav_MenuOpen\"]\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-nav-label-close=\"@L[\"Nav_MenuClose\"]\"", layout, StringComparison.Ordinal);
        Assert.Contains("/admin/admin.js", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"language-switcher\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin/culture", layout, StringComparison.Ordinal);
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
        Assert.Contains("placeholder=\"@L[\"Identities_SearchUnavailable\"]\"", page, StringComparison.Ordinal);
        Assert.Contains("disabled", page, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"identity-search-note\"", page, StringComparison.Ordinal);
        Assert.Contains("identity-search-note", page, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanAndEnglishResourceKeysAreSymmetric()
    {
        var german = ReadResourceKeys("SharedResource.de.resx");
        var english = ReadResourceKeys("SharedResource.en.resx");

        Assert.Equal(german.OrderBy(key => key), english.OrderBy(key => key));
    }

    private static string ReadUiSource(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "UiSource", fileName));

    private static IReadOnlySet<string> ReadResourceKeys(string fileName) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "UiSource", fileName))
            .Descendants("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
}
