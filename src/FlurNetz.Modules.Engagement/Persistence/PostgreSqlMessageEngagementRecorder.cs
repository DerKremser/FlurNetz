using FlurNetz.Messaging.Integration;
using FlurNetz.Messaging.Persistence;
using FlurNetz.Modules.Engagement.Application;
using FlurNetz.Modules.Engagement.Domain;
using FlurNetz.Persistence.Connections;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Modules.Engagement.Persistence;

/// <summary>
/// Persistiert eine Message-Aktivität gemeinsam mit ihrem Integration Event in der Outbox.
/// </summary>
/// <remarks>
/// Der Publisher führt keinen eigenen Commit aus. Deshalb liegen Activity-INSERT und
/// Outbox-INSERT absichtlich in derselben <see cref="PostgreSqlTransaction"/>. Fällt der
/// zweite Schritt aus, darf auch die bereits eingefügte Aktivität nicht sichtbar bleiben.
/// </remarks>
public sealed class PostgreSqlMessageEngagementRecorder : IMessageEngagementRecorder
{
    private readonly IPostgreSqlConnectionFactory connectionFactory;
    private readonly EngagementActivityRepository repository;
    private readonly IIntegrationEventPublisher publisher;

    /// <summary>
    /// Erstellt den atomaren Message-Recorder.
    /// </summary>
    /// <param name="connectionFactory">Fabrik für PostgreSQL-Verbindungen.</param>
    /// <param name="repository">Der bestehende Engagement-Adapter für den Activity-INSERT.</param>
    /// <param name="publisher">Der bestehende Messaging-Outbox-Publisher.</param>
    /// <exception cref="ArgumentNullException">Wenn eine Abhängigkeit fehlt.</exception>
    public PostgreSqlMessageEngagementRecorder(
        IPostgreSqlConnectionFactory connectionFactory,
        EngagementActivityRepository repository,
        IIntegrationEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(publisher);
        this.connectionFactory = connectionFactory;
        this.repository = repository;
        this.publisher = publisher;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        EngagementActivity activity,
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(envelope);

        await using var transaction = await PostgreSqlTransaction
            .BeginAsync(connectionFactory, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await repository.AddAsync(activity, transaction, cancellationToken).ConfigureAwait(false);
            await publisher.EnqueueAsync(transaction, envelope, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
