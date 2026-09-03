using System.Data.Common;
using FlurNetz.Modules.Overlay.Contracts;
using FlurNetz.Modules.Overlay.Domain;

namespace FlurNetz.Modules.Overlay.Application;

/// <summary>Rotiert den technischen Browser-Source-Key atomar.</summary>
public sealed class RotateOverlaySourceKey(IOverlayChannelStore store)
{
    private readonly IOverlayChannelStore store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Gibt den neuen Klartext-Key genau einmal zurück.</summary>
    public async Task<OverlayChannelSecret?> ExecuteAsync(OverlayChannelId channelId, CancellationToken cancellationToken = default)
    {
        var key = OverlaySourceKey.Generate();
        var channel = await store.MutateAsync(channelId, current =>
        {
            if (current.IsArchived) throw new OverlayChannelArchivedException(current.Id);
            return false;
        }, OverlaySourceKey.Hash(key), cancellationToken: cancellationToken).ConfigureAwait(false);
        return channel is null ? null : new OverlayChannelSecret(channel, key);
    }

    /// <summary>Rotiert den Key innerhalb einer vom Kompositor gehaltenen Transaktion.</summary>
    public async Task<OverlayChannelSecret?> ExecuteAsync(
        OverlayChannelId channelId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var key = OverlaySourceKey.Generate();
        var channel = await store.MutateAsync(
                channelId,
                current =>
                {
                    if (current.IsArchived) throw new OverlayChannelArchivedException(current.Id);
                    return false;
                },
                connection,
                transaction,
                replacementSourceKeyHash: OverlaySourceKey.Hash(key),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return channel is null ? null : new OverlayChannelSecret(channel, key);
    }
}
