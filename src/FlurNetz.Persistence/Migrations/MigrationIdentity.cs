namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Beschreibt die stabile Identität einer SQL-Migration.
/// </summary>
/// <remarks>
/// <c>Owner</c> erlaubt mehreren Quellen, gemeinsam Migrationen bereitzustellen.
/// Zusammen mit <c>Version</c> bildet er den technischen Schlüssel der History-Tabelle;
/// <c>Name</c> macht die Migration für Menschen lesbar und wird ebenfalls auf Änderungen geprüft.
/// </remarks>
public sealed record MigrationIdentity
{
    /// <summary>
    /// Erstellt eine Migration-Identität mit Besitzer, positiver Version und Namen.
    /// </summary>
    /// <param name="owner">Stabiler, fachlich oder technisch verantwortlicher Besitzer.</param>
    /// <param name="version">Positive, innerhalb des Besitzers monotone Versionsnummer.</param>
    /// <param name="name">Lesbarer und stabiler Name der Migration.</param>
    /// <exception cref="ArgumentException">Wenn Besitzer oder Name fehlen.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn die Version nicht positiv ist.</exception>
    public MigrationIdentity(string owner, long version, string name)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("A migration owner is required.", nameof(owner));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A migration version must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A migration name is required.", nameof(name));
        }

        Owner = owner;
        Version = version;
        Name = name;
    }

    /// <summary>
    /// Gibt den stabilen Besitzer dieser Migration zurück.
    /// </summary>
    public string Owner { get; }

    /// <summary>
    /// Gibt die positive Versionsnummer innerhalb des Besitzers zurück.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gibt den lesbaren Namen dieser Migration zurück.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Formatiert die Identität deterministisch als <c>Owner:Version:Name</c>.
    /// </summary>
    public override string ToString() => $"{Owner}:{Version}:{Name}";
}
