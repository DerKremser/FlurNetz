using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Shop.Migrations;

/// <summary>
/// Liefert die fachlichen PostgreSQL-Migrationen des Shop-Moduls.
/// </summary>
public sealed class ShopMigrationSource : IMigrationSource
{
    private const string CreateShopOffersSql = """
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

    private const string CreateShopPurchasesSql = """
        CREATE TABLE IF NOT EXISTS shop_purchase_requests
        (
            request_id uuid NOT NULL,
            shop_purchase_id uuid NOT NULL,
            shop_offer_id uuid NOT NULL,
            community_identity_id uuid NOT NULL,

            CONSTRAINT pk_shop_purchase_requests
                PRIMARY KEY (request_id),

            CONSTRAINT uq_shop_purchase_requests_purchase
                UNIQUE (shop_purchase_id)
        );

        CREATE TABLE IF NOT EXISTS shop_purchase_guards
        (
            shop_offer_id uuid NOT NULL,
            community_identity_id uuid NOT NULL,

            CONSTRAINT pk_shop_purchase_guards
                PRIMARY KEY (shop_offer_id, community_identity_id)
        );

        CREATE TABLE IF NOT EXISTS shop_purchases
        (
            id uuid NOT NULL,
            shop_offer_id uuid NOT NULL,
            community_identity_id uuid NOT NULL,
            purchased_inventory_item_definition_id uuid NOT NULL,
            price_paid bigint NOT NULL,
            purchased_at timestamptz NOT NULL,

            CONSTRAINT pk_shop_purchases
                PRIMARY KEY (id),

            CONSTRAINT fk_shop_purchases_shop_offer
                FOREIGN KEY (shop_offer_id)
                REFERENCES shop_offers (id)
                ON DELETE RESTRICT,

            CONSTRAINT ck_shop_purchases_price_paid_non_negative
                CHECK (price_paid >= 0)
        );

        CREATE INDEX IF NOT EXISTS ix_shop_purchases_offer_identity
            ON shop_purchases (shop_offer_id, community_identity_id);

        CREATE INDEX IF NOT EXISTS ix_shop_purchases_identity_purchased_at
            ON shop_purchases (community_identity_id, purchased_at);
        """;

    private const string AddShopOfferSortOrderSql = """
        ALTER TABLE shop_offers
            ADD COLUMN sort_order integer DEFAULT 0;

        ALTER TABLE shop_offers
            ALTER COLUMN sort_order SET NOT NULL;

        ALTER TABLE shop_offers
            ALTER COLUMN sort_order DROP DEFAULT;

        ALTER TABLE shop_offers
            ADD CONSTRAINT ck_shop_offers_sort_order_non_negative
                CHECK (sort_order >= 0);
        """;

    /// <summary>
    /// Gibt den persistierten Angebotskatalog und die atomare Purchase-Persistenz zurück.
    /// </summary>
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Shop", 1, "CreateShopOffers", CreateShopOffersSql);
        yield return new Migration("Shop", 2, "CreateShopPurchases", CreateShopPurchasesSql);
        yield return new Migration("Shop", 3, "AddShopOfferSortOrder", AddShopOfferSortOrderSql);
    }
}
