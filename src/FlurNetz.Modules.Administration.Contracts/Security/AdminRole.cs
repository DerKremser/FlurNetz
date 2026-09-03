namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Versioniertes V1-Rollenbundle.</summary>
public static class AdminRole
{
    public const string Administrator = "Administrator";
    public const int PermissionBundleVersion = 1;
    public static IReadOnlySet<string> AdministratorPermissions => PermissionCatalog.All;
}
