using FlurNetz.Messaging.Integration;
using FlurNetz.Persistence.Transactions;

namespace FlurNetz.Messaging.Persistence;

/// <summary>
/// Persistiert Integration Events innerhalb einer vorhandenen PostgreSQL-Transaktion.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Enqueued eine Nachricht dauerhaft in die Outbox.
    /// </summary>
    /// <param name="transaction">Die bestehende Business-Transaktion.</param>
    /// <param name="envelope">Die zu persistierende Nachricht.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Datenbankbefehls.</param>
    /// <returns>Eine Aufgabe für die abgeschlossene Persistierung.</returns>
    /// <remarks>
    /// Die Methode führt keinen eigenen Commit aus. Erst ein Commit von
    /// <paramref name="transaction"/> macht Business Write und Outbox-Eintrag gemeinsam dauerhaft.
    /// </remarks>
    Task EnqueueAsync(
        PostgreSqlTransaction transaction,
        IntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default);
}
