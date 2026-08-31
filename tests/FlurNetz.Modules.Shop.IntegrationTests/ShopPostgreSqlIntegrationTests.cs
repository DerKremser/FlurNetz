using Dapper;
using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Application;
using FlurNetz.Modules.Shop.Contracts;
using FlurNetz.Modules.Shop.Domain;
using FlurNetz.Modules.Shop.Migrations;
using FlurNetz.Modules.Shop.Persistence;
using FlurNetz.Persistence.Configuration;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Migrations;
using Npgsql;

namespace FlurNetz.Modules.Shop.IntegrationTests;

/// <summary>
/// Prüft Migration, Schema, Domain-Rehydration, Katalog-Use-Cases und Row-Lock-Nebenläufigkeit.
/// </summary>
public sealed class ShopPostgreSqlIntegrationTests(ShopPostgreSqlFixture database)
    : IClassFixture<ShopPostgreSqlFixture>
{
    private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ShopMigrationCreatesExactlyItsCatalogTableAndIsIdempotent()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await ResetShopMigrationAsync(factory);

        var migrationSource = new ShopMigrationSource();
        var migration = Assert.Single(migrationSource.GetMigrations());
        var runner = new MigrationRunner(factory, migrationSource);

        var firstRun = await runner.RunAsync(TestToken);
        var secondRun = await runner.RunAsync(TestToken);

        Assert.Equal(new MigrationRunResult(1, 0), firstRun);
        Assert.Equal(new MigrationRunResult(0, 1), secondRun);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var shopRelations = (await connection.QueryAsync<ShopRelation>(
                new CommandDefinition(
                    """
                    SELECT relation_row.relname AS RelationName,
                           relation_row.relkind::text AS RelationKind
                    FROM pg_class relation_row
                    JOIN pg_namespace namespace_row
                      ON namespace_row.oid = relation_row.relnamespace
                    WHERE namespace_row.nspname = 'public'
                      AND substring(relation_row.relname, 1, 5) = 'shop_'
                    ORDER BY relation_row.relname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var userTriggerCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_trigger trigger_row
                JOIN pg_class relation_row
                  ON relation_row.oid = trigger_row.tgrelid
                JOIN pg_namespace namespace_row
                  ON namespace_row.oid = relation_row.relnamespace
                WHERE namespace_row.nspname = 'public'
                  AND relation_row.relname = 'shop_offers'
                  AND NOT trigger_row.tgisinternal;
                """,
                cancellationToken: TestToken));
        var history = await connection.QuerySingleAsync<MigrationHistory>(
            new CommandDefinition(
                $"""
                SELECT owner AS Owner, version AS Version, name AS Name, checksum AS Checksum
                FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Shop' AND version = 1;
                """,
                cancellationToken: TestToken));

        var shopRelation = Assert.Single(shopRelations);
        Assert.Equal("shop_offers", shopRelation.RelationName);
        Assert.Equal("r", shopRelation.RelationKind);
        Assert.DoesNotContain(shopRelations, relation =>
            relation.RelationName.Contains("purchase", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, userTriggerCount);
        Assert.Equal("Shop", history.Owner);
        Assert.Equal(1, history.Version);
        Assert.Equal("CreateShopOffers", history.Name);
        Assert.Equal(MigrationChecksum.Compute(migration.Sql), history.Checksum);
        Assert.NotEmpty(history.Checksum);
    }

    [Fact]
    public async Task ShopOffersHaveTheExactSchemaWithoutForeignKeysOrDefaults()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        var columns = (await connection.QueryAsync<ColumnInfo>(
                new CommandDefinition(
                    """
                    SELECT column_name AS ColumnName,
                           data_type AS DataType,
                           is_nullable AS IsNullable,
                           character_maximum_length AS CharacterMaximumLength,
                           column_default AS ColumnDefault
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'shop_offers'
                    ORDER BY ordinal_position;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();
        var foreignKeyCount = await connection.QuerySingleAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pg_constraint
                WHERE conrelid = to_regclass('shop_offers') AND contype = 'f';
                """,
                cancellationToken: TestToken));
        var checkConstraints = (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT conname
                    FROM pg_constraint
                    WHERE conrelid = to_regclass('shop_offers') AND contype = 'c'
                    ORDER BY conname;
                    """,
                    cancellationToken: TestToken)))
            .ToArray();

        Assert.Equal(
            [
                "id",
                "item_definition_id",
                "display_name",
                "description",
                "price",
                "is_enabled",
                "available_from",
                "available_until",
                "purchase_limit_per_identity"
            ],
            columns.Select(column => column.ColumnName).ToArray());
        Assert.Equal("uuid", columns[0].DataType);
        Assert.Equal("uuid", columns[1].DataType);
        Assert.Equal("character varying", columns[2].DataType);
        Assert.Equal(200, columns[2].CharacterMaximumLength);
        Assert.Equal("character varying", columns[3].DataType);
        Assert.Equal(2000, columns[3].CharacterMaximumLength);
        Assert.Equal("bigint", columns[4].DataType);
        Assert.Equal("boolean", columns[5].DataType);
        Assert.Equal("timestamp with time zone", columns[6].DataType);
        Assert.Equal("timestamp with time zone", columns[7].DataType);
        Assert.Equal("integer", columns[8].DataType);
        Assert.Equal(["NO", "NO", "NO", "YES", "NO", "NO", "YES", "YES", "YES"],
            columns.Select(column => column.IsNullable).ToArray());
        Assert.All(columns, column => Assert.Null(column.ColumnDefault));
        Assert.Equal("id", await ReadPrimaryKeyAsync(connection));
        Assert.Equal(0, foreignKeyCount);
        Assert.Equal(
            [
                "ck_shop_offers_availability_ordered",
                "ck_shop_offers_description_not_blank",
                "ck_shop_offers_description_trimmed",
                "ck_shop_offers_display_name_not_blank",
                "ck_shop_offers_display_name_trimmed",
                "ck_shop_offers_price_non_negative",
                "ck_shop_offers_purchase_limit_positive"
            ],
            checkConstraints);
    }

    [Fact]
    public async Task DirectInvalidWritesAreRejectedAndValidBoundaryValuesAreAccepted()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);

        var invalidWrites = new[]
        {
            new RawOffer(price: -1),
            new RawOffer(purchaseLimitPerIdentity: 0),
            new RawOffer(purchaseLimitPerIdentity: -1),
            new RawOffer(availableFrom: Utc(12), availableUntil: Utc(12)),
            new RawOffer(availableFrom: Utc(13), availableUntil: Utc(12)),
            new RawOffer(displayName: "\u2003\u00a0"),
            new RawOffer(displayName: " Angebot"),
            new RawOffer(description: "\u2003\u00a0"),
            new RawOffer(description: " Beschreibung"),
            new RawOffer(displayName: new string('a', 201)),
            new RawOffer(description: new string('b', 2001)),
            new RawOffer(displayName: RepeatUnicodeScalar("😀", 201)),
            new RawOffer(description: RepeatUnicodeScalar("🧪", 2001)),
            new RawOffer(displayName: "Angebot\0intern"),
            new RawOffer(description: "Beschreibung\0intern")
        };

        foreach (var invalidWrite in invalidWrites)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => InsertRawAsync(factory, invalidWrite));
        }

        var validWrites = new[]
        {
            new RawOffer(price: 0),
            new RawOffer(purchaseLimitPerIdentity: null),
            new RawOffer(purchaseLimitPerIdentity: 1),
            new RawOffer(availableFrom: Utc(12)),
            new RawOffer(availableUntil: Utc(12)),
            new RawOffer(availableFrom: Utc(12), availableUntil: Utc(13)),
            new RawOffer(description: null),
            new RawOffer(
                displayName: RepeatUnicodeScalar("😀", 200),
                description: RepeatUnicodeScalar("🧪", 2000))
        };

        foreach (var validWrite in validWrites)
        {
            await InsertRawAsync(factory, validWrite);
        }

        await using var connection = await factory.OpenConnectionAsync(TestToken);
        Assert.Equal(validWrites.Length, await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM shop_offers;", cancellationToken: TestToken)));
    }

    [Fact]
    public async Task StoreRoundtripsAllFieldsAndUnknownGetReturnsNull()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var store = new ShopOfferStore(factory);
        var id = ShopOfferId.New();
        var itemDefinitionId = ItemDefinitionId.New();
        var from = new DateTimeOffset(2026, 8, 31, 14, 0, 0, TimeSpan.FromHours(2)).AddTicks(10);
        var until = new DateTimeOffset(2026, 8, 31, 13, 0, 0, TimeSpan.Zero).AddTicks(20);
        var offer = ShopOffer.Create(
            id,
            itemDefinitionId,
            " Angebot 😀 ",
            " Beschreibung 🧪 ",
            ShopPrice.Create(42),
            AvailabilityWindow.Create(from, until),
            3);
        offer.Enable();

        await store.AddAsync(offer, TestToken);

        var loaded = await store.GetAsync(id, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(id, loaded!.Id);
        Assert.Equal(itemDefinitionId, loaded.ItemDefinitionId);
        Assert.Equal("Angebot 😀", loaded.DisplayName);
        Assert.Equal("Beschreibung 🧪", loaded.Description);
        Assert.Equal(ShopPrice.Create(42), loaded.Price);
        Assert.True(loaded.IsEnabled);
        Assert.Equal(offer.Availability, loaded.Availability);
        Assert.Equal(offer.Availability.AvailableFrom, loaded.Availability.AvailableFrom);
        Assert.Equal(offer.Availability.AvailableUntil, loaded.Availability.AvailableUntil);
        Assert.Equal(3, loaded.PurchaseLimitPerIdentity);
        Assert.Null(await store.GetAsync(ShopOfferId.New(), TestToken));

        var freeOffer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Kostenlos",
            null,
            ShopPrice.Zero,
            AvailabilityWindow.Create(null, null),
            null);
        await store.AddAsync(freeOffer, TestToken);
        var loadedFreeOffer = await store.GetAsync(freeOffer.Id, TestToken);
        Assert.Equal(ShopPrice.Zero, loadedFreeOffer!.Price);
        Assert.Null(loadedFreeOffer.Description);
        Assert.Null(loadedFreeOffer.PurchaseLimitPerIdentity);
        Assert.False(loadedFreeOffer.IsEnabled);
    }

    [Fact]
    public async Task StoreListsOffersDeterministicallyById()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var store = new ShopOfferStore(factory);
        var firstId = ShopOfferId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = ShopOfferId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        await store.AddAsync(ShopOffer.Create(secondId, ItemDefinitionId.New(), "B"), TestToken);
        await store.AddAsync(ShopOffer.Create(firstId, ItemDefinitionId.New(), "A"), TestToken);

        var offers = await store.ListAsync(TestToken);

        Assert.Equal([firstId, secondId], offers.Select(offer => offer.Id).ToArray());
        Assert.NotEqual(offers[0].ItemDefinitionId, offers[1].ItemDefinitionId);
    }

    [Fact]
    public async Task CatalogUseCasesPersistMutationsAndNoOps()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var store = new ShopOfferStore(factory);
        var create = new CreateShopOffer(store);
        var offer = await create.ExecuteAsync(
            ItemDefinitionId.New(),
            "Angebot",
            "Alt",
            ShopPrice.Create(10),
            AvailabilityWindow.Create(null, null),
            2,
            TestToken);

        Assert.False(offer.IsEnabled);
        Assert.True(await new RenameShopOffer(store).ExecuteAsync(offer.Id, "Neu", TestToken));
        Assert.False(await new RenameShopOffer(store).ExecuteAsync(offer.Id, "  Neu  ", TestToken));
        Assert.True(await new ChangeShopOfferDescription(store).ExecuteAsync(offer.Id, null, TestToken));
        Assert.True(await new ChangeShopOfferPrice(store).ExecuteAsync(offer.Id, ShopPrice.Zero, TestToken));
        Assert.True(await new ChangeShopOfferAvailability(store).ExecuteAsync(
            offer.Id,
            AvailabilityWindow.Create(Utc(12), Utc(13)),
            TestToken));
        Assert.True(await new ChangeShopOfferPurchaseLimit(store).ExecuteAsync(offer.Id, null, TestToken));
        Assert.True(await new EnableShopOffer(store).ExecuteAsync(offer.Id, TestToken));
        Assert.False(await new EnableShopOffer(store).ExecuteAsync(offer.Id, TestToken));
        Assert.True(await new DisableShopOffer(store).ExecuteAsync(offer.Id, TestToken));

        var loaded = await new GetShopOffer(store).ExecuteAsync(offer.Id, TestToken);
        Assert.NotNull(loaded);
        Assert.Equal("Neu", loaded!.DisplayName);
        Assert.Null(loaded.Description);
        Assert.Equal(ShopPrice.Zero, loaded.Price);
        Assert.Equal(AvailabilityWindow.Create(Utc(12), Utc(13)), loaded.Availability);
        Assert.Null(loaded.PurchaseLimitPerIdentity);
        Assert.False(loaded.IsEnabled);
    }

    [Fact]
    public async Task MutationCallbackExceptionRollsBackThePersistedOffer()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var store = new ShopOfferStore(factory);
        var id = ShopOfferId.New();
        var original = ShopOffer.Create(
            id,
            ItemDefinitionId.New(),
            "Original",
            "Ursprünglich",
            ShopPrice.Create(11),
            AvailabilityWindow.Create(Utc(12).AddTicks(10), Utc(13).AddTicks(10)),
            2);
        original.Enable();

        await store.AddAsync(original, TestToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteAsync(
            id,
            offer => MutateThenThrow(offer),
            TestToken));

        var loaded = await store.GetAsync(id, TestToken);

        Assert.NotNull(loaded);
        Assert.Equal(original.Id, loaded!.Id);
        Assert.Equal(original.ItemDefinitionId, loaded.ItemDefinitionId);
        Assert.Equal(original.DisplayName, loaded.DisplayName);
        Assert.Equal(original.Description, loaded.Description);
        Assert.Equal(original.Price, loaded.Price);
        Assert.Equal(original.IsEnabled, loaded.IsEnabled);
        Assert.Equal(original.Availability, loaded.Availability);
        Assert.Equal(original.PurchaseLimitPerIdentity, loaded.PurchaseLimitPerIdentity);
    }

    [Fact]
    public async Task DomainNoOpDoesNotUpdateTheDatabaseRow()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var store = new ShopOfferStore(factory);
        var offer = ShopOffer.Create(
            ShopOfferId.New(),
            ItemDefinitionId.New(),
            "Angebot",
            "Beschreibung",
            ShopPrice.Create(7),
            AvailabilityWindow.Create(Utc(12), Utc(13)),
            2);

        await store.AddAsync(offer, TestToken);
        var beforeXmin = await ReadXminAsync(factory, offer.Id);

        var changed = await new RenameShopOffer(store).ExecuteAsync(
            offer.Id,
            "  Angebot  ",
            TestToken);

        var afterXmin = await ReadXminAsync(factory, offer.Id);
        var loaded = await store.GetAsync(offer.Id, TestToken);

        Assert.False(changed);
        Assert.Equal(beforeXmin, afterXmin);
        Assert.NotNull(loaded);
        Assert.Equal(offer.DisplayName, loaded!.DisplayName);
        Assert.Equal(offer.Description, loaded.Description);
        Assert.Equal(offer.Price, loaded.Price);
        Assert.Equal(offer.IsEnabled, loaded.IsEnabled);
        Assert.Equal(offer.Availability, loaded.Availability);
        Assert.Equal(offer.PurchaseLimitPerIdentity, loaded.PurchaseLimitPerIdentity);
    }

    [Fact]
    public async Task AtomicMutationOfUnknownOfferThrowsNotFound()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);

        await Assert.ThrowsAsync<ShopOfferNotFoundException>(() =>
            new EnableShopOffer(new ShopOfferStore(factory)).ExecuteAsync(ShopOfferId.New(), TestToken));
    }

    [Fact]
    public async Task ConcurrentMutationOfOneOfferWaitsForObservedRowLockWithoutLostFields()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await using var observerFactory = CreateFactory();
        await PrepareShopAsync(factory);
        var id = ShopOfferId.New();
        await new ShopOfferStore(factory).AddAsync(
            ShopOffer.Create(id, ItemDefinitionId.New(), "A", "Alt"),
            TestToken);

        var firstApplicationName = $"shop-same-a-{id.Value:N}";
        var secondApplicationName = $"shop-same-b-{id.Value:N}";
        await using var firstFactory = CreateFactory(firstApplicationName);
        await using var secondFactory = CreateFactory(secondApplicationName);
        var firstCallbackEntered = CreateSignal();
        var secondCallbackEntered = CreateSignal();
        var releaseFirst = CreateSignal();
        Task<bool>? firstTask = null;
        Task<bool>? secondTask = null;

        try
        {
            firstTask = Task.Run(() => new ShopOfferStore(firstFactory).ExecuteAsync(
                id,
                offer =>
                {
                    firstCallbackEntered.TrySetResult(true);
                    releaseFirst.Task.GetAwaiter().GetResult();
                    return offer.Rename("A geändert");
                },
                TestToken));

            await firstCallbackEntered.Task.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken);

            secondTask = Task.Run(() => new ShopOfferStore(secondFactory).ExecuteAsync(
                id,
                offer =>
                {
                    secondCallbackEntered.TrySetResult(true);
                    return offer.ChangeDescription("B geändert");
                },
                TestToken));

            await WaitForRowLockWaitAsync(
                observerFactory,
                secondApplicationName);
            Assert.False(secondCallbackEntered.Task.IsCompleted);

            releaseFirst.TrySetResult(true);
            Assert.True(await firstTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken));
            Assert.True(await secondTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            releaseFirst.TrySetResult(true);
            await DrainTaskAsync(firstTask);
            await DrainTaskAsync(secondTask);
        }

        var loaded = await new ShopOfferStore(factory).GetAsync(id, TestToken);
        Assert.Equal("A geändert", loaded!.DisplayName);
        Assert.Equal("B geändert", loaded.Description);
    }

    [Fact]
    public async Task ConcurrentMutationOfDifferentOffersCommitsBeforeTheHeldOfferIsReleased()
    {
        SkipIfDatabaseIsUnavailable();
        await using var factory = CreateFactory();
        await PrepareShopAsync(factory);
        var firstId = ShopOfferId.New();
        var secondId = ShopOfferId.New();
        var store = new ShopOfferStore(factory);
        await store.AddAsync(ShopOffer.Create(firstId, ItemDefinitionId.New(), "A"), TestToken);
        await store.AddAsync(ShopOffer.Create(secondId, ItemDefinitionId.New(), "B"), TestToken);

        await using var firstFactory = CreateFactory($"shop-different-a-{firstId.Value:N}");
        await using var secondFactory = CreateFactory($"shop-different-b-{secondId.Value:N}");
        var firstCallbackEntered = CreateSignal();
        var secondCallbackEntered = CreateSignal();
        var releaseFirst = CreateSignal();
        Task<bool>? firstTask = null;
        Task<bool>? secondTask = null;

        try
        {
            firstTask = Task.Run(() => new ShopOfferStore(firstFactory).ExecuteAsync(
                firstId,
                offer =>
                {
                    firstCallbackEntered.TrySetResult(true);
                    releaseFirst.Task.GetAwaiter().GetResult();
                    return offer.Rename("A geändert");
                },
                TestToken));

            await firstCallbackEntered.Task.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken);

            secondTask = Task.Run(() => new ShopOfferStore(secondFactory).ExecuteAsync(
                secondId,
                offer =>
                {
                    secondCallbackEntered.TrySetResult(true);
                    return offer.Rename("B geändert");
                },
                TestToken));

            await secondCallbackEntered.Task.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken);
            Assert.True(await secondTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken));
            Assert.False(releaseFirst.Task.IsCompleted);
            Assert.Equal("B geändert", (await store.GetAsync(secondId, TestToken))!.DisplayName);

            releaseFirst.TrySetResult(true);
            Assert.True(await firstTask.WaitAsync(
                ConcurrencyTimeout,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            releaseFirst.TrySetResult(true);
            await DrainTaskAsync(firstTask);
            await DrainTaskAsync(secondTask);
        }

        Assert.Equal("A geändert", (await store.GetAsync(firstId, TestToken))!.DisplayName);
        Assert.Equal("B geändert", (await store.GetAsync(secondId, TestToken))!.DisplayName);
    }

    private PostgreSqlConnectionFactory CreateFactory(string? applicationName = null)
    {
        var connectionString = database.ConnectionString;
        if (applicationName is not null)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = applicationName
            };
            connectionString = builder.ConnectionString;
        }

        return new PostgreSqlConnectionFactory(new PostgreSqlOptions(connectionString));
    }

    private static bool MutateThenThrow(ShopOffer offer)
    {
        offer.Rename("Verändert");
        offer.ChangeDescription("Andere Beschreibung");
        offer.ChangePrice(99);
        offer.ChangeAvailability(AvailabilityWindow.Create(Utc(14), Utc(15)));
        offer.ChangePurchaseLimit(null);
        offer.Disable();
        throw new InvalidOperationException("Absichtlicher Testfehler nach der Mutation.");
    }

    private static async Task<long> ReadXminAsync(
        PostgreSqlConnectionFactory factory,
        ShopOfferId shopOfferId)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        return await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                "SELECT xmin::text::bigint FROM shop_offers WHERE id = @Id;",
                new { Id = shopOfferId.Value },
                cancellationToken: TestToken));
    }

    private static async Task WaitForRowLockWaitAsync(
        PostgreSqlConnectionFactory observerFactory,
        string applicationName)
    {
        await using var connection = await observerFactory.OpenConnectionAsync(TestToken);
        var deadline = DateTime.UtcNow + ConcurrencyTimeout;

        while (DateTime.UtcNow < deadline)
        {
            var isWaitingForLock = await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    """
                    SELECT EXISTS
                    (
                        SELECT 1
                        FROM pg_stat_activity
                        WHERE application_name = @ApplicationName
                          AND backend_type = 'client backend'
                          AND state = 'active'
                          AND wait_event_type = 'Lock'
                    );
                    """,
                    new { ApplicationName = applicationName },
                    cancellationToken: TestToken));

            if (isWaitingForLock)
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Die Verbindung {applicationName} wartete nicht auf einen PostgreSQL-Row-Lock.");
    }

    private static TaskCompletionSource<bool> CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task DrainTaskAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Die Hauptassertionsfläche berichtet die eigentliche Testursache.
        }
    }

    private void SkipIfDatabaseIsUnavailable()
    {
        Assert.SkipUnless(database.IsAvailable, database.SkipReason);
    }

    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    private static async Task PrepareShopAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new ShopMigrationSource()).RunAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM shop_offers;", cancellationToken: TestToken));
    }

    private static async Task ResetShopMigrationAsync(PostgreSqlConnectionFactory factory)
    {
        await new MigrationRunner(factory, new ShopMigrationSource()).RunAsync(TestToken);
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
                DROP TABLE IF EXISTS shop_offers;
                DELETE FROM {MigrationRunner.MigrationHistoryTableName}
                WHERE owner = 'Shop' AND version = 1;
                """,
                cancellationToken: TestToken));
    }

    private static async Task InsertRawAsync(
        PostgreSqlConnectionFactory factory,
        RawOffer rawOffer)
    {
        await using var connection = await factory.OpenConnectionAsync(TestToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO shop_offers
                    (id, item_definition_id, display_name, description, price, is_enabled,
                     available_from, available_until, purchase_limit_per_identity)
                VALUES
                    (@Id, @ItemDefinitionId, @DisplayName, @Description, @Price, @IsEnabled,
                     @AvailableFrom, @AvailableUntil, @PurchaseLimitPerIdentity);
                """,
                new
                {
                    rawOffer.Id,
                    rawOffer.ItemDefinitionId,
                    rawOffer.DisplayName,
                    rawOffer.Description,
                    rawOffer.Price,
                    rawOffer.IsEnabled,
                    rawOffer.AvailableFrom,
                    rawOffer.AvailableUntil,
                    rawOffer.PurchaseLimitPerIdentity
                },
                cancellationToken: TestToken));
    }

    private static Task<string> ReadPrimaryKeyAsync(System.Data.Common.DbConnection connection) =>
        connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT string_agg(attribute.attname, ',' ORDER BY key_column.ordinality)
                FROM pg_constraint constraint_row
                CROSS JOIN LATERAL unnest(constraint_row.conkey) WITH ORDINALITY AS key_column(attnum, ordinality)
                JOIN pg_attribute attribute
                  ON attribute.attrelid = constraint_row.conrelid
                 AND attribute.attnum = key_column.attnum
                WHERE constraint_row.conrelid = to_regclass('shop_offers')
                  AND constraint_row.contype = 'p';
                """,
                cancellationToken: TestToken));

    private static DateTimeOffset Utc(int hour) =>
        new(2026, 8, 31, hour, 0, 0, TimeSpan.Zero);

    private static string RepeatUnicodeScalar(string scalar, int count) =>
        string.Concat(Enumerable.Repeat(scalar, count));

    private sealed class ShopRelation
    {
        public string RelationName { get; set; } = string.Empty;

        public string RelationKind { get; set; } = string.Empty;
    }

    private sealed class RawOffer
    {
        public RawOffer(
            Guid id = default,
            Guid itemDefinitionId = default,
            string displayName = "Angebot",
            string? description = "Beschreibung",
            long price = 1,
            bool isEnabled = false,
            DateTimeOffset? availableFrom = null,
            DateTimeOffset? availableUntil = null,
            int? purchaseLimitPerIdentity = null)
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id;
            ItemDefinitionId = itemDefinitionId == Guid.Empty ? Guid.NewGuid() : itemDefinitionId;
            DisplayName = displayName;
            Description = description;
            Price = price;
            IsEnabled = isEnabled;
            AvailableFrom = availableFrom;
            AvailableUntil = availableUntil;
            PurchaseLimitPerIdentity = purchaseLimitPerIdentity;
        }

        public Guid Id { get; }

        public Guid ItemDefinitionId { get; }

        public string DisplayName { get; }

        public string? Description { get; }

        public long Price { get; }

        public bool IsEnabled { get; }

        public DateTimeOffset? AvailableFrom { get; }

        public DateTimeOffset? AvailableUntil { get; }

        public int? PurchaseLimitPerIdentity { get; }
    }

    private sealed class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string IsNullable { get; set; } = string.Empty;

        public int? CharacterMaximumLength { get; set; }

        public string? ColumnDefault { get; set; }
    }

    private sealed class MigrationHistory
    {
        public string Owner { get; set; } = string.Empty;

        public long Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;
    }
}
