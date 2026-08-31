using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Inventory.Migrations;

/// <summary>
/// Liefert die fachliche PostgreSQL-Migration des ersten persistierten Inventory-Slices.
/// </summary>
/// <remarks>
/// Inventory bleibt Eigentümer seiner Tabelle. Die CommunityIdentityId ist ein fachlicher
/// Cross-Module-Identifier und besitzt deshalb keinen Foreign Key auf Identity. Die
/// ItemDefinitionId gehört zum öffentlichen Inventory-Contract; ein Item-Katalog wird in diesem
/// Slice bewusst nicht eingeführt.
/// </remarks>
public sealed class InventoryMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS community_inventory_entries
        (
            community_identity_id uuid NOT NULL,
            item_definition_id uuid NOT NULL,
            quantity bigint NOT NULL,
            CONSTRAINT pk_community_inventory_entries
                PRIMARY KEY (community_identity_id, item_definition_id),
            CONSTRAINT ck_community_inventory_entries_quantity_non_negative
                CHECK (quantity >= 0)
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige fachliche Inventory-Migration zurück.
    /// </summary>
    /// <returns>Die Migration zur Anlage der Community-Inventory-Tabelle.</returns>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Inventory", 1, "CreateCommunityInventoryEntries", MigrationSql);
    }
}
