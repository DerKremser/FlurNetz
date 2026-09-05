using FlurNetz.Persistence.Migrations;

namespace FlurNetz.Modules.Administration.Migrations;

/// <summary>Unveränderliche SQL-first-Migration für den eigenen Administrationszustand.</summary>
public sealed class AdministrationMigrationSource : IMigrationSource
{
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS administration_credentials
        (
            community_identity_id uuid PRIMARY KEY,
            email varchar(320) NOT NULL,
            normalized_email varchar(320) NOT NULL,
            password_hash text NOT NULL,
            credential_version bigint NOT NULL,
            created_at_utc timestamptz(6) NOT NULL,
            password_changed_at_utc timestamptz(6) NOT NULL,
            CONSTRAINT ux_administration_credentials_normalized_email UNIQUE (normalized_email),
            CONSTRAINT ck_administration_credentials_version_positive CHECK (credential_version > 0),
            CONSTRAINT ck_administration_credentials_email_length CHECK (char_length(email) BETWEEN 3 AND 320),
            CONSTRAINT ck_administration_credentials_email_trimmed CHECK (email = btrim(email)),
            CONSTRAINT ck_administration_credentials_hash_not_blank CHECK (btrim(password_hash) <> '')
        );

        CREATE TABLE IF NOT EXISTS administration_setup_state
        (
            id smallint PRIMARY KEY,
            completed_at_utc timestamptz(6) NULL,
            CONSTRAINT ck_administration_setup_state_singleton CHECK (id = 1)
        );

        INSERT INTO administration_setup_state (id)
        VALUES (1)
        ON CONFLICT (id) DO NOTHING;

        CREATE TABLE IF NOT EXISTS administration_role_assignments
        (
            community_identity_id uuid NOT NULL,
            role_name varchar(100) NOT NULL,
            created_at_utc timestamptz(6) NOT NULL,
            CONSTRAINT pk_administration_role_assignments PRIMARY KEY (community_identity_id, role_name),
            CONSTRAINT ck_administration_role_assignments_role_not_blank CHECK (btrim(role_name) <> '')
        );

        CREATE TABLE IF NOT EXISTS administration_audit_entries
        (
            id uuid PRIMARY KEY,
            actor_community_identity_id uuid NOT NULL,
            actor_identity_snapshot varchar(100) NOT NULL,
            action varchar(150) NOT NULL,
            target_type varchar(150) NOT NULL,
            target_id varchar(200) NOT NULL,
            target_display_snapshot varchar(500) NULL,
            risk_level varchar(20) NOT NULL,
            reason varchar(1000) NULL,
            result varchar(30) NOT NULL,
            occurred_at_utc timestamptz(6) NOT NULL,
            correlation_id varchar(200) NOT NULL,
            request_id uuid NULL,
            failure_code varchar(200) NULL,
            change_summary jsonb NOT NULL,
            metadata jsonb NOT NULL,
            CONSTRAINT ck_administration_audit_action_not_blank CHECK (btrim(action) <> ''),
            CONSTRAINT ck_administration_audit_target_type_not_blank CHECK (btrim(target_type) <> ''),
            CONSTRAINT ck_administration_audit_target_id_not_blank CHECK (btrim(target_id) <> ''),
            CONSTRAINT ck_administration_audit_risk CHECK (risk_level IN ('Low', 'Medium', 'High')),
            CONSTRAINT ck_administration_audit_result CHECK (result IN ('Succeeded', 'Rejected', 'Failed', 'OutcomeUnknown'))
        );

        CREATE INDEX IF NOT EXISTS ix_administration_audit_occurred
            ON administration_audit_entries (occurred_at_utc DESC, id DESC);
        CREATE INDEX IF NOT EXISTS ix_administration_audit_actor
            ON administration_audit_entries (actor_community_identity_id, occurred_at_utc DESC, id DESC);

        CREATE TABLE IF NOT EXISTS administration_operations
        (
            request_id uuid PRIMARY KEY,
            actor_community_identity_id uuid NOT NULL,
            operation_type varchar(150) NOT NULL,
            target_type varchar(150) NOT NULL,
            target_id varchar(200) NOT NULL,
            request_fingerprint char(64) NOT NULL,
            correlation_id varchar(200) NOT NULL,
            mutation_status varchar(30) NOT NULL,
            audit_status varchar(30) NOT NULL,
            created_at_utc timestamptz(6) NOT NULL,
            completed_at_utc timestamptz(6) NULL,
            CONSTRAINT ck_administration_operations_fingerprint CHECK (request_fingerprint ~ '^[0-9a-f]{64}$'),
            CONSTRAINT ck_administration_operations_mutation_status CHECK (mutation_status IN ('Reserved', 'Succeeded', 'Rejected', 'Failed', 'OutcomeUnknown')),
            CONSTRAINT ck_administration_operations_audit_status CHECK (audit_status IN ('Pending', 'Succeeded', 'Failed'))
        );

        CREATE INDEX IF NOT EXISTS ix_administration_operations_actor
            ON administration_operations (actor_community_identity_id, created_at_utc DESC);
        """;

    public IEnumerable<Migration> GetMigrations()
    {
        yield return new Migration("Administration", 1, "CreateAdministrationSecurityState", MigrationSql);
        yield return new Migration(
            "Administration",
            2,
            "AddAdministratorPreferredCulture",
            """
            ALTER TABLE administration_credentials
                ADD COLUMN IF NOT EXISTS preferred_culture varchar(2) NULL;

            ALTER TABLE administration_credentials
                ADD CONSTRAINT ck_administration_credentials_preferred_culture
                CHECK (preferred_culture IS NULL OR preferred_culture IN ('de', 'en'));
            """);
    }
}
