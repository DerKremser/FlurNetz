namespace FlurNetz.Modules.Identity.Contracts;

/// <summary>
/// Bezeichnet die zentrale interne Identität eines Community-Mitglieds innerhalb von FlurNetz.
/// Externe Plattformkennungen werden an Integrationsgrenzen auf diesen Wert aufgelöst und
/// ersetzen ihn nicht als zentrale Identität.
/// </summary>
public readonly record struct CommunityIdentityId
{
    private readonly Guid _value;

    private CommunityIdentityId(Guid value)
    {
        _value = value;
    }

    /// <summary>
    /// Liefert den stabilen GUID-Wert der internen Community-Identität.
    /// </summary>
    public Guid Value => _value;

    /// <summary>
    /// Erstellt eine interne Identitätskennung aus einer nicht leeren GUID.
    /// </summary>
    /// <param name="value">Die der internen Identität zugeordnete GUID.</param>
    /// <returns>Eine unveränderliche interne Identitätskennung.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="value"/> leer ist.</exception>
    public static CommunityIdentityId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Die interne Community-Identity-ID darf nicht leer sein.",
                nameof(value));
        }

        return new CommunityIdentityId(value);
    }

    /// <summary>
    /// Erzeugt eine neue interne Identitätskennung.
    /// </summary>
    /// <returns>Eine neue unveränderliche interne Identitätskennung.</returns>
    public static CommunityIdentityId New() => Create(Guid.NewGuid());
}
