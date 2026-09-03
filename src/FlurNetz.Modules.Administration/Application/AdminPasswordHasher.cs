using Microsoft.AspNetCore.Identity;

namespace FlurNetz.Modules.Administration.Application;

public interface IAdminPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string passwordHash, string password);
}

/// <summary>Adapter um Microsofts etabliertes Identity-Passworthashing.</summary>
public sealed class AdminPasswordHasher : IAdminPasswordHasher
{
    private readonly PasswordHasher<object> hasher = new();

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return hasher.HashPassword(new object(), password);
    }

    public PasswordVerificationResult Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);
        return hasher.VerifyHashedPassword(new object(), passwordHash, password);
    }
}
