namespace FlurNetz.Modules.Administration.Contracts.Security;

/// <summary>Benennung der expliziten Permission-Policies.</summary>
public static class AdminPolicies
{
    public const string Access = "Admin.Administration.Access";
    public const string DashboardRead = "Admin.Administration.Dashboard.Read";

    public static string ForPermission(string permission) =>
        string.IsNullOrWhiteSpace(permission)
            ? throw new ArgumentException("Eine Permission ist erforderlich.", nameof(permission))
            : $"Admin.{permission}";
}
