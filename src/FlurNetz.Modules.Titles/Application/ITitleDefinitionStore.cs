using System.Data.Common;
using FlurNetz.Modules.Titles.Domain;

namespace FlurNetz.Modules.Titles.Application;

/// <summary>
/// Definiert die interne Persistenzgrenze für den Titles-Definitionskatalog.
/// </summary>
/// <remarks>
/// Der synchrone Callback enthält ausschließlich Domain-Logik. Dadurch kann während der
/// offenen Mutationstransaktion keine beliebige externe I/O ausgeführt werden.
/// </remarks>
public interface ITitleDefinitionStore
{
    /// <summary>
    /// Persistiert eine neue Title-Definition.
    /// </summary>
    Task AddAsync(
        TitleDefinition definition,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TitleDefinition definition,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");

    /// <summary>
    /// Lädt eine Title-Definition oder liefert bei unbekannter ID <see langword="null"/>.
    /// </summary>
    Task<TitleDefinition?> GetAsync(
        TitleDefinitionId titleDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt alle Title-Definitionen in technisch deterministischer ID-Reihenfolge.
    /// </summary>
    Task<IReadOnlyList<TitleDefinition>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lädt und mutiert eine Definition atomar über einen synchronen Domain-Callback.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        TitleDefinitionId titleDefinitionId,
        Func<TitleDefinition, TResult> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(
        TitleDefinitionId titleDefinitionId,
        Func<TitleDefinition, TResult> operation,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Dieser Store unterstützt keinen externen Transaktionskontext.");
}
