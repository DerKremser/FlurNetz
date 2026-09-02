using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Automation.Migrations;

/// <summary>
/// Liefert die einzige unveränderliche PostgreSQL-V1-Migration der Automation.
/// </summary>
public sealed class AutomationMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS automation_rules
        (
            id uuid NOT NULL,
            display_name varchar(100) NOT NULL,
            description varchar(500) NULL,
            trigger_type varchar(100) NOT NULL,
            sort_order integer NOT NULL,
            is_enabled boolean NOT NULL,
            is_archived boolean NOT NULL,
            created_at_utc timestamptz(6) NOT NULL,
            updated_at_utc timestamptz(6) NOT NULL,
            CONSTRAINT pk_automation_rules PRIMARY KEY (id),
            CONSTRAINT ck_automation_rules_sort_order_non_negative CHECK (sort_order >= 0),
            CONSTRAINT ck_automation_rules_not_archived_and_enabled CHECK (NOT (is_archived AND is_enabled)),
            CONSTRAINT ck_automation_rules_trigger_type CHECK (
                trigger_type IN ('engagement.message-recorded', 'shop.purchase-completed')
            ),
            CONSTRAINT ck_automation_rules_display_name_not_blank CHECK (
                btrim(display_name, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
            ),
            CONSTRAINT ck_automation_rules_display_name_trimmed CHECK (
                display_name = btrim(display_name, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
            ),
            CONSTRAINT ck_automation_rules_description_not_blank CHECK (
                description IS NULL OR btrim(description, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
            ),
            CONSTRAINT ck_automation_rules_description_trimmed CHECK (
                description IS NULL OR description = btrim(description, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
            ),
            CONSTRAINT ck_automation_rules_updated_after_created CHECK (updated_at_utc >= created_at_utc)
        );

        CREATE INDEX IF NOT EXISTS ix_automation_rules_runtime
            ON automation_rules (trigger_type, is_enabled, is_archived, sort_order, id);

        CREATE TABLE IF NOT EXISTS automation_rule_conditions
        (
            automation_rule_id uuid NOT NULL,
            position integer NOT NULL,
            condition_type varchar(100) NOT NULL,
            community_identity_id uuid NULL,
            shop_offer_id uuid NULL,
            item_definition_id uuid NULL,
            amount bigint NULL,
            CONSTRAINT pk_automation_rule_conditions PRIMARY KEY (automation_rule_id, position),
            CONSTRAINT uq_automation_rule_conditions_type UNIQUE (automation_rule_id, condition_type),
            CONSTRAINT fk_automation_rule_conditions_rule FOREIGN KEY (automation_rule_id)
                REFERENCES automation_rules (id) ON DELETE CASCADE,
            CONSTRAINT ck_automation_rule_conditions_position CHECK (position BETWEEN 0 AND 15),
            CONSTRAINT ck_automation_rule_conditions_type CHECK (
                condition_type IN (
                    'community-identity.equals',
                    'shop.offer-id.equals',
                    'shop.item-definition-id.equals',
                    'shop.price-paid.at-least',
                    'shop.price-paid.at-most'
                )
            ),
            CONSTRAINT ck_automation_rule_conditions_amount_non_negative CHECK (amount IS NULL OR amount >= 0),
            CONSTRAINT ck_automation_rule_conditions_ids_not_empty CHECK (
                (community_identity_id IS NULL OR community_identity_id <> '00000000-0000-0000-0000-000000000000')
                AND (shop_offer_id IS NULL OR shop_offer_id <> '00000000-0000-0000-0000-000000000000')
                AND (item_definition_id IS NULL OR item_definition_id <> '00000000-0000-0000-0000-000000000000')
            ),
            CONSTRAINT ck_automation_rule_conditions_value_shape CHECK (
                (condition_type = 'community-identity.equals'
                    AND community_identity_id IS NOT NULL AND shop_offer_id IS NULL AND item_definition_id IS NULL AND amount IS NULL)
                OR (condition_type = 'shop.offer-id.equals'
                    AND community_identity_id IS NULL AND shop_offer_id IS NOT NULL AND item_definition_id IS NULL AND amount IS NULL)
                OR (condition_type = 'shop.item-definition-id.equals'
                    AND community_identity_id IS NULL AND shop_offer_id IS NULL AND item_definition_id IS NOT NULL AND amount IS NULL)
                OR (condition_type IN ('shop.price-paid.at-least', 'shop.price-paid.at-most')
                    AND community_identity_id IS NULL AND shop_offer_id IS NULL AND item_definition_id IS NULL AND amount IS NOT NULL)
            )
        );

        CREATE TABLE IF NOT EXISTS automation_rule_actions
        (
            automation_rule_id uuid NOT NULL,
            position integer NOT NULL,
            action_type varchar(100) NOT NULL,
            amount bigint NULL,
            notification_title varchar(200) NULL,
            notification_message varchar(2000) NULL,
            CONSTRAINT pk_automation_rule_actions PRIMARY KEY (automation_rule_id, position),
            CONSTRAINT fk_automation_rule_actions_rule FOREIGN KEY (automation_rule_id)
                REFERENCES automation_rules (id) ON DELETE CASCADE,
            CONSTRAINT ck_automation_rule_actions_position CHECK (position BETWEEN 0 AND 15),
            CONSTRAINT ck_automation_rule_actions_type CHECK (action_type IN ('economy.credit', 'notification.create')),
            CONSTRAINT ck_automation_rule_actions_value_shape CHECK (
                (action_type = 'economy.credit'
                    AND amount IS NOT NULL AND amount > 0
                    AND notification_title IS NULL AND notification_message IS NULL)
                OR (action_type = 'notification.create'
                    AND amount IS NULL AND notification_title IS NOT NULL
                    AND btrim(notification_title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
                    AND notification_title = btrim(notification_title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'))
            ),
            CONSTRAINT ck_automation_rule_actions_message_not_blank CHECK (
                notification_message IS NULL OR btrim(notification_message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
            ),
            CONSTRAINT ck_automation_rule_actions_message_trimmed CHECK (
                notification_message IS NULL OR notification_message = btrim(notification_message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
            )
        );

        CREATE TABLE IF NOT EXISTS automation_executions
        (
            id uuid NOT NULL,
            automation_rule_id uuid NOT NULL,
            trigger_message_id uuid NOT NULL,
            trigger_message_type varchar(100) NOT NULL,
            trigger_schema_version integer NOT NULL,
            community_identity_id uuid NOT NULL,
            trigger_occurred_at_utc timestamptz(6) NOT NULL,
            executed_at_utc timestamptz(6) NOT NULL,
            CONSTRAINT pk_automation_executions PRIMARY KEY (id),
            CONSTRAINT uq_automation_executions_rule_message UNIQUE (automation_rule_id, trigger_message_id),
            CONSTRAINT fk_automation_executions_rule FOREIGN KEY (automation_rule_id)
                REFERENCES automation_rules (id) ON DELETE RESTRICT,
            CONSTRAINT ck_automation_executions_trigger_version_v1 CHECK (trigger_schema_version = 1),
            CONSTRAINT ck_automation_executions_ids_not_empty CHECK (
                id <> '00000000-0000-0000-0000-000000000000'
                AND automation_rule_id <> '00000000-0000-0000-0000-000000000000'
                AND trigger_message_id <> '00000000-0000-0000-0000-000000000000'
                AND community_identity_id <> '00000000-0000-0000-0000-000000000000'
            ),
            CONSTRAINT ck_automation_executions_trigger_type CHECK (
                trigger_message_type IN ('engagement.message-recorded', 'shop.purchase-completed')
            )
        );

        CREATE INDEX IF NOT EXISTS ix_automation_executions_history
            ON automation_executions (automation_rule_id, executed_at_utc DESC, id DESC);
        """;

    /// <inheritdoc />
    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration(
            "Automation",
            1,
            "CreateAutomationRulesAndExecutions",
            MigrationSql);
        yield return new Migration(
            "Automation",
            2,
            "AddOverlayAlertAction",
            OverlayAlertMigrationSql);
    }

    private const string OverlayAlertMigrationSql = """
        ALTER TABLE automation_rule_actions
            ADD COLUMN IF NOT EXISTS overlay_channel_id uuid NULL,
            ADD COLUMN IF NOT EXISTS overlay_title varchar(200) NULL,
            ADD COLUMN IF NOT EXISTS overlay_message varchar(2000) NULL,
            ADD COLUMN IF NOT EXISTS overlay_variant varchar(20) NULL,
            ADD COLUMN IF NOT EXISTS overlay_duration_milliseconds integer NULL;

        ALTER TABLE automation_rule_actions
            DROP CONSTRAINT IF EXISTS ck_automation_rule_actions_type,
            DROP CONSTRAINT IF EXISTS ck_automation_rule_actions_value_shape;

        ALTER TABLE automation_rule_actions
            ADD CONSTRAINT ck_automation_rule_actions_type_v2 CHECK (
                action_type IN ('economy.credit', 'notification.create', 'overlay.alert')
            ),
            ADD CONSTRAINT ck_automation_rule_actions_value_shape_v2 CHECK (
                (action_type = 'economy.credit'
                    AND amount IS NOT NULL AND amount > 0
                    AND notification_title IS NULL AND notification_message IS NULL
                    AND overlay_channel_id IS NULL AND overlay_title IS NULL
                    AND overlay_message IS NULL AND overlay_variant IS NULL
                    AND overlay_duration_milliseconds IS NULL)
                OR (action_type = 'notification.create'
                    AND amount IS NULL AND notification_title IS NOT NULL
                    AND notification_title = btrim(notification_title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
                    AND overlay_channel_id IS NULL AND overlay_title IS NULL
                    AND overlay_message IS NULL AND overlay_variant IS NULL
                    AND overlay_duration_milliseconds IS NULL)
                OR (action_type = 'overlay.alert'
                    AND amount IS NULL AND notification_title IS NULL AND notification_message IS NULL
                    AND overlay_channel_id IS NOT NULL
                    AND overlay_channel_id <> '00000000-0000-0000-0000-000000000000'
                    AND overlay_title IS NOT NULL
                    AND btrim(overlay_title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
                    AND overlay_title = btrim(overlay_title, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')
                    AND (overlay_message IS NULL OR (
                        btrim(overlay_message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000') <> ''
                        AND overlay_message = btrim(overlay_message, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000')))
                    AND overlay_variant IN ('default', 'success', 'warning', 'celebration')
                    AND overlay_duration_milliseconds BETWEEN 1000 AND 30000)
            );
        """;
}
