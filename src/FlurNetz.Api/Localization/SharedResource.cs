using System.Globalization;
using System.Resources;

namespace FlurNetz.Api;

/// <summary>
/// Gemeinsamer Ressourcenanker für die lokalisierte Administration.
/// </summary>
public sealed class SharedResource
{
    private static readonly ResourceManager Resources = new(
        "FlurNetz.Api.Resources.SharedResource",
        typeof(SharedResource).Assembly);

    public static string Validation_EmailRequired => Get(nameof(Validation_EmailRequired));
    public static string Validation_EmailInvalid => Get(nameof(Validation_EmailInvalid));
    public static string Validation_PasswordRequired => Get(nameof(Validation_PasswordRequired));
    public static string Validation_PasswordConfirmationRequired => Get(nameof(Validation_PasswordConfirmationRequired));
    public static string Validation_PasswordConfirmationMismatch => Get(nameof(Validation_PasswordConfirmationMismatch));
    public static string Validation_SetupSecretRequired => Get(nameof(Validation_SetupSecretRequired));
    public static string Validation_CurrentPasswordRequired => Get(nameof(Validation_CurrentPasswordRequired));
    public static string Validation_NewPasswordRequired => Get(nameof(Validation_NewPasswordRequired));

    private static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
