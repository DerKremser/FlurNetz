using System.Data.Common;
using FlurNetz.Modules.Economy.Contracts;
using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Application;

/// <summary>
/// Adapter der öffentlichen Economy-Debit-Fähigkeit auf den bestehenden Economy-Store.
/// </summary>
public sealed class EconomyBalanceDebit : IEconomyBalanceDebit
{
    private readonly ICommunityEconomyStore store;

    /// <summary>
    /// Erstellt den transaction-aware Economy-Debit-Adapter.
    /// </summary>
    public EconomyBalanceDebit(ICommunityEconomyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc />
    public async Task DebitAsync(
        CommunityIdentityId communityIdentityId,
        long amount,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        _ = await store.DebitAsync(
                communityIdentityId,
                amount,
                connection,
                transaction,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
