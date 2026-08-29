namespace FlurNetz.Persistence.Migrations;

public interface IMigrationSource
{
    IEnumerable<Migration> GetMigrations();
}
