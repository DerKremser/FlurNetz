namespace FlurNetz.Api.IntegrationTests;

public sealed class AdminPasswordGeneratorUiTests
{
    [Fact]
    public void GeneratorUsesOnlyTheCryptographicallySecureBrowserRandomSource()
    {
        var script = ReadUiSource("admin.js");

        Assert.Contains("window.crypto.getRandomValues", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.random", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("document.cookie", script, StringComparison.Ordinal);
        Assert.DoesNotContain("console.", script, StringComparison.Ordinal);
        Assert.DoesNotContain("location.search", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Account.cshtml")]
    [InlineData("Setup.cshtml")]
    public void PasswordPagesExposeTheGeneratorWithoutPersistingGeneratedValues(string pageName)
    {
        var page = ReadUiSource(pageName);

        Assert.Contains("data-password-generator", page, StringComparison.Ordinal);
        Assert.Contains("data-password-generator-password", page, StringComparison.Ordinal);
        Assert.Contains("data-password-generator-confirm", page, StringComparison.Ordinal);
        Assert.Contains("data-password-toggle", page, StringComparison.Ordinal);
        Assert.Contains("data-password-copy", page, StringComparison.Ordinal);
        Assert.Contains("Sicheres Passwort generieren", page, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", page, StringComparison.Ordinal);
        Assert.Contains("@Html.AntiForgeryToken()", page, StringComparison.Ordinal);
    }

    private static string ReadUiSource(string fileName) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "UiSource", fileName));
}
