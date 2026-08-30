using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Rewards.Migrations;

/// <summary>
/// Liefert die erste fachliche Rewards-Migration für Konfiguration und Grant-Records.
/// </summary>
/// <remarks>
/// Rewards besitzt die Tabellen selbst. Die CommunityIdentityId bleibt dabei ein
/// Cross-Module-Identifier ohne Foreign Key; atomare Zusammenarbeit mit Economy erfolgt
/// über den öffentlichen Capability-Contract und eine gemeinsame Transaktion.
/// </remarks>
public sealed class RewardsMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS reward_definitions
        (
            id uuid PRIMARY KEY,
            definition_type text NOT NULL,
            amount bigint NOT NULL,
            CONSTRAINT ck_reward_definitions_amount_positive
                CHECK (amount > 0)
        );

        CREATE TABLE IF NOT EXISTS reward_packages
        (
            id uuid PRIMARY KEY
        );

        CREATE TABLE IF NOT EXISTS reward_package_definitions
        (
            reward_package_id uuid NOT NULL,
            reward_definition_id uuid NOT NULL,
            CONSTRAINT pk_reward_package_definitions
                PRIMARY KEY (reward_package_id, reward_definition_id),
            CONSTRAINT fk_reward_package_definitions_package
                FOREIGN KEY (reward_package_id) REFERENCES reward_packages (id),
            CONSTRAINT fk_reward_package_definitions_definition
                FOREIGN KEY (reward_definition_id) REFERENCES reward_definitions (id)
        );

        CREATE TABLE IF NOT EXISTS reward_grants
        (
            id uuid PRIMARY KEY,
            community_identity_id uuid NOT NULL,
            reward_definition_id uuid NOT NULL,
            source_type text NOT NULL,
            source_id text NOT NULL,
            CONSTRAINT fk_reward_grants_definition
                FOREIGN KEY (reward_definition_id) REFERENCES reward_definitions (id),
            CONSTRAINT uq_reward_grants_source_definition
                UNIQUE (source_type, source_id, reward_definition_id),
            CONSTRAINT ck_reward_grants_source_type_not_blank
                CHECK (btrim(source_type) <> ''),
            CONSTRAINT ck_reward_grants_source_id_not_blank
                CHECK (btrim(source_id) <> '')
        );
        """;

    /// <summary>
    /// Gibt die Rewards-Migration Version 1 zurück.
    /// </summary>
    /// <returns>Die Migration für Definitionen, Packages, Membership und Grants.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration(
            "Rewards",
            1,
            "CreateRewardConfigurationAndGrants",
            MigrationSql);
    }
}
