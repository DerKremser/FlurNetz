using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Shop.Migrations;

/// <summary>
/// Liefert die erste fachliche PostgreSQL-Migration des Shop-Moduls.
/// </summary>
/// <remarks>
/// Der Shop ist Eigentümer seiner Angebotskatalogtabelle. Die ItemDefinitionId bleibt ein
/// stabiler Cross-Module-Identifier ohne Foreign Key; Käufe, Economy, Inventory Grant,
/// Messaging und weitere Zukunftstabellen gehören ausdrücklich nicht zu dieser Migration.
/// </remarks>
public sealed class ShopMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS shop_offers
        (
            id uuid NOT NULL,
            item_definition_id uuid NOT NULL,
            display_name varchar(200) NOT NULL,
            description varchar(2000) NULL,
            price bigint NOT NULL,
            is_enabled boolean NOT NULL,
            available_from timestamptz NULL,
            available_until timestamptz NULL,
            purchase_limit_per_identity integer NULL,

            CONSTRAINT pk_shop_offers
                PRIMARY KEY (id),

            CONSTRAINT ck_shop_offers_display_name_not_blank
                CHECK (
                    btrim(
                        display_name,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    ) <> ''
                ),

            CONSTRAINT ck_shop_offers_display_name_trimmed
                CHECK (
                    display_name = btrim(
                        display_name,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    )
                ),

            CONSTRAINT ck_shop_offers_description_not_blank
                CHECK (
                    description IS NULL
                    OR btrim(
                        description,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    ) <> ''
                ),

            CONSTRAINT ck_shop_offers_description_trimmed
                CHECK (
                    description IS NULL
                    OR description = btrim(
                        description,
                        U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'
                    )
                ),

            CONSTRAINT ck_shop_offers_price_non_negative
                CHECK (price >= 0),

            CONSTRAINT ck_shop_offers_purchase_limit_positive
                CHECK (
                    purchase_limit_per_identity IS NULL
                    OR purchase_limit_per_identity > 0
                ),

            CONSTRAINT ck_shop_offers_availability_ordered
                CHECK (
                    available_from IS NULL
                    OR available_until IS NULL
                    OR available_from < available_until
                )
        );
        """;

    /// <summary>
    /// Gibt die erste und derzeit einzige Shop-Migration zurück.
    /// </summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Shop", 1, "CreateShopOffers", MigrationSql);
    }
}
