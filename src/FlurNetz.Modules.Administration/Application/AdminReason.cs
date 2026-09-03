namespace FlurNetz.Modules.Administration.Application;

public static class AdminReason
{
    public const int MaximumLength = 1000;

    public static string? Optional(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var canonical = reason.Trim();
        if (canonical.Length > MaximumLength)
        {
            throw new ArgumentException($"Der Reason darf höchstens {MaximumLength} Zeichen lang sein.", nameof(reason));
        }

        return canonical;
    }

    public static string Required(string? reason) =>
        Optional(reason) ?? throw new ArgumentException("Für diese High-Risk-Aktion ist ein Reason erforderlich.", nameof(reason));
}
