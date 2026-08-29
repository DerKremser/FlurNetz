namespace FlurNetz.BuildingBlocks.Guards;

public static class Guard
{
    public static T NotNull<T>(T? value, string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Der Wert darf nicht leer oder aus Leerzeichen bestehen.", parameterName)
            : value;
    }
}
