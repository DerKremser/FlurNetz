using System.Data.Common;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Application;

/// <summary>
/// Adapter der öffentlichen Economy-Credit-Fähigkeit auf den bestehenden Economy-Store.
/// </summary>
/// <remarks>
/// Der Adapter führt keine zweite fachliche Logik ein. Die Domain-Rehydration, das
/// <c>SELECT FOR UPDATE</c> und das Update bleiben ausschließlich im Economy-Store.
/// </remarks>
public sealed class EconomyBalanceCredit : IEconomyBalanceCredit
{
    private readonly ICommunityEconomyStore store;

    /// <summary>
    /// Erstellt den transaction-aware Economy-Adapter.
    /// </summary>
    /// <param name="store">Der bestehende atomare Economy-Store.</param>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="store"/> fehlt.</exception>
    public EconomyBalanceCredit(ICommunityEconomyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc />
    public async Task CreditAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        _ = await store.CreditAsync(
                communityIdentityId,
                amount,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
