using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Economy.Domain;

/// <summary>
/// Hält den neutral modellierten Economy-Saldo genau einer Community-Identität.
/// </summary>
/// <remarks>
/// Economy verwendet die bestehende <see cref="CommunityIdentityId"/> als zentrale interne
/// Identität und führt keine zweite Benutzerkennung ein. Der Zustand startet bei null;
/// konkrete Währungsnamen, mehrere Währungen und externe Plattformidentitäten gehören nicht
/// in diese Foundation.
/// </remarks>
public sealed class CommunityEconomy
{
    private CommunityEconomy(CommunityIdentityId communityIdentityId)
    {
        CommunityIdentityId = communityIdentityId;
        Balance = EconomyBalance.Zero;
    }

    /// <summary>
    /// Liefert die unveränderliche interne Community-Identität, der der Saldo gehört.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert den aktuellen nicht-negativen Economy-Saldo.
    /// </summary>
    public EconomyBalance Balance { get; private set; }

    /// <summary>
    /// Erzeugt einen Economy-Zustand mit dem Anfangssaldo null.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <returns>Ein neuer Economy-Zustand für die angegebene Community-Identität.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="communityIdentityId"/> leer ist.</exception>
    public static CommunityEconomy Create(CommunityIdentityId communityIdentityId)
    {
        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Community-Economy benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }

        return new CommunityEconomy(communityIdentityId);
    }

    /// <summary>
    /// Schreibt einen positiven Betrag fachlich gültig gut.
    /// </summary>
    /// <param name="amount">Der gutzuschreibende positive Betrag.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="OverflowException">Wenn die Gutschrift den maximalen <see cref="long"/>-Wert überschreiten würde.</exception>
    public void Credit(long amount)
    {
        Balance = Balance.Credit(amount);
    }

    /// <summary>
    /// Bucht einen positiven Betrag ohne Überziehung fachlich gültig ab.
    /// </summary>
    /// <param name="amount">Der abzubuchende positive Betrag.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="InsufficientEconomyBalanceException">Wenn der Saldo für die Abbuchung nicht ausreicht.</exception>
    public void Debit(long amount)
    {
        Balance = Balance.Debit(amount);
    }
}
