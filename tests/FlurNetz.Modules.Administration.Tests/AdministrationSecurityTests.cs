using FlurNetz.Modules.Administration.Application;
using FlurNetz.Modules.Administration.Contracts.Operations;
using FlurNetz.Modules.Administration.Contracts.Security;
using FlurNetz.Modules.Administration.Domain;

namespace FlurNetz.Modules.Administration.Tests;

public sealed class AdministrationSecurityTests
{
    [Fact]
    public void PermissionCatalogContainsTheCompleteVersionOneBundle()
    {
        Assert.Equal(30, PermissionCatalog.All.Count);
        Assert.Contains(PermissionCatalog.Access, PermissionCatalog.All);
        Assert.Contains(PermissionCatalog.OverlayRotateSourceKey, PermissionCatalog.All);
        Assert.DoesNotContain("Administration.Zugriff", PermissionCatalog.All);
    }

    [Fact]
    public void AdministratorRoleIsTheStaticVersionOnePermissionBundle()
    {
        Assert.Equal("Administrator", AdminRole.Administrator);
        Assert.Equal(1, AdminRole.PermissionBundleVersion);
        Assert.Same(PermissionCatalog.All, AdminRole.AdministratorPermissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    public void EmailRejectsMissingAndTooShortValues(string? value)
    {
        Assert.Throws<ArgumentException>(() => AdminEmail.Normalize(value));
    }

    [Fact]
    public void EmailNormalizationIsCaseInsensitiveButKeepsTheCanonicalDisplayValue()
    {
        Assert.Equal("Admin.Operator@example.com", AdminEmail.Canonicalize("  Admin.Operator@example.com  "));
        Assert.Equal("ADMIN.OPERATOR@EXAMPLE.COM", AdminEmail.Normalize("  Admin.Operator@example.com  "));
    }

    [Fact]
    public void PasswordPolicyAcceptsLongPassphrasesWithoutChangingTheValue()
    {
        var password = "  lange Passphrase mit Unicode ✓  ";
        AdminPasswordPolicy.Validate(password);
        Assert.NotEqual(password.Trim(), password);
        Assert.Contains("✓", password, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void PasswordPolicyRejectsPasswordsShorterThanFifteenCharacters(string password)
    {
        Assert.Throws<ArgumentException>(() => AdminPasswordPolicy.Validate(password));
    }

    [Fact]
    public void FingerprintIsIndependentOfInputOrdering()
    {
        var first = AdminRequestFingerprint.Compute(("amount", 10), ("identity", "id"));
        var second = AdminRequestFingerprint.Compute(("identity", "id"), ("amount", 10));
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void HighRiskReasonIsRequiredAndCanonicalized()
    {
        Assert.Equal("operator correction", AdminReason.Required("  operator correction  "));
        Assert.Throws<ArgumentException>(() => AdminReason.Required("  "));
    }

    [Fact]
    public void CredentialVersionStartsAtOneAndIncrementsOnPasswordChange()
    {
        var id = FlurNetz.Modules.Identity.Contracts.CommunityIdentityId.New();
        var credential = AdminCredential.Create(id, "operator@example.com", "hash", DateTimeOffset.UtcNow);
        Assert.Equal(1, credential.CredentialVersion);
        credential.ChangePassword("new-hash", DateTimeOffset.UtcNow);
        Assert.Equal(2, credential.CredentialVersion);
    }

    [Fact]
    public void PreferredCultureAllowsOnlyGermanAndEnglishAndDefaultsToGerman()
    {
        var id = FlurNetz.Modules.Identity.Contracts.CommunityIdentityId.New();
        var credential = AdminCredential.Create(id, "operator@example.com", "hash", DateTimeOffset.UtcNow);

        Assert.Null(credential.PreferredCulture);
        Assert.Equal(AdminPreferredCulture.Default, AdminPreferredCulture.Resolve(credential.PreferredCulture));
        credential.SetPreferredCulture(" EN ");
        Assert.Equal(AdminPreferredCulture.English, credential.PreferredCulture);
        Assert.Throws<ArgumentException>(() => credential.SetPreferredCulture("fr"));
        Assert.Equal(AdminPreferredCulture.Default, AdminPreferredCulture.Resolve("invalid"));
    }
}
