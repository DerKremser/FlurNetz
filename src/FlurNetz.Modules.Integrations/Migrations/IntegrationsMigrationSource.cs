using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Integrations.Migrations;

/// <summary>
/// Liefert die erste fachliche Migration des Integrationsmoduls.
/// </summary>
/// <remarks>
/// Integrations ist Eigentümer seiner Mappingtabelle. Die Community-Identity-ID bleibt ein
/// fachlicher Identifier ohne Cross-Module-Foreign-Key.
/// </remarks>
public sealed class IntegrationsMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS integration_external_identity_mappings
        (
            provider_key varchar(64) NOT NULL,
            external_user_id varchar(256) NOT NULL,
            community_identity_id uuid NOT NULL,
            CONSTRAINT pk_integration_external_identity_mappings
                PRIMARY KEY (provider_key, external_user_id),
            CONSTRAINT ck_integration_external_identity_mappings_provider_key_not_blank
                CHECK (length(provider_key) > 0),
            CONSTRAINT ck_integration_external_identity_mappings_external_user_id_not_blank
                CHECK (length(external_user_id) > 0),
            CONSTRAINT ck_integration_external_identity_mappings_community_identity_id_not_empty
                CHECK (community_identity_id <> '00000000-0000-0000-0000-000000000000')
        );

        CREATE INDEX IF NOT EXISTS ix_integration_external_identity_mappings_community_identity
            ON integration_external_identity_mappings
                (community_identity_id, provider_key, external_user_id);
        """;

    /// <summary>Gibt die Migration zur Anlage der Mappingtabelle zurück.</summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration(
            "Integrations",
            1,
            "CreateExternalIdentityMappings",
            MigrationSql);
    }
}
