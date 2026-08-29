namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Beschreibt eine unveränderliche, explizit definierte SQL-Migration.
/// </summary>
/// <remarks>
/// Der Runner sortiert Migrationen über ihre Identität und speichert zusätzlich einen
/// SHA-256-Hash des exakten SQL-Texts. Dadurch werden nachträgliche Änderungen an bereits
/// angewendeten Migrationen erkannt.
/// </remarks>
public sealed class Migration
{
    /// <summary>
    /// Erstellt eine Migration aus ihren Identitätsfeldern und dem SQL-Text.
    /// </summary>
    /// <param name="owner">Stabiler Besitzer der Migration.</param>
    /// <param name="version">Positive Version innerhalb des Besitzers.</param>
    /// <param name="name">Stabiler, lesbarer Name.</param>
    /// <param name="sql">Explizit auszuführender SQL-Text.</param>
    /// <exception cref="ArgumentException">Wenn Name, Besitzer oder SQL fehlen.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn die Version nicht positiv ist.</exception>
    public Migration(string owner, long version, string name, string sql)
        : this(new MigrationIdentity(owner, version, name), sql)
    {
    }

    /// <summary>
    /// Erstellt eine Migration aus einer bereits gebildeten Identität und dem SQL-Text.
    /// </summary>
    /// <param name="identity">Stabile Identität der Migration.</param>
    /// <param name="sql">Explizit auszuführender SQL-Text.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="identity"/> fehlt.</exception>
    /// <exception cref="ArgumentException">Wenn der SQL-Text fehlt.</exception>
    public Migration(MigrationIdentity identity, string sql)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("Migration SQL is required.", nameof(sql));
        }

        Identity = identity;
        Sql = sql;
    }

    /// <summary>
    /// Gibt die unveränderliche Identität der Migration zurück.
    /// </summary>
    public MigrationIdentity Identity { get; }

    /// <summary>
    /// Gibt den Besitzer der Migration zurück.
    /// </summary>
    public string Owner => Identity.Owner;

    /// <summary>
    /// Gibt die Version der Migration zurück.
    /// </summary>
    public long Version => Identity.Version;

    /// <summary>
    /// Gibt den Namen der Migration zurück.
    /// </summary>
    public string Name => Identity.Name;

    /// <summary>
    /// Gibt den unveränderten SQL-Text zurück, der für Ausführung und Hash verwendet wird.
    /// </summary>
    public string Sql { get; }
}
