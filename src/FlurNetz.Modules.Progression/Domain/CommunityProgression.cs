using FlurNetz.Modules.Identity.Contracts;

namespace FlurNetz.Modules.Progression.Domain;

/// <summary>
/// Hält den fachlichen Progressionszustand genau einer Community-Identität.
/// </summary>
/// <remarks>
/// Als Benutzeridentität wird die bestehende <see cref="CommunityIdentityId"/>
/// aus den Identity-Verträgen wiederverwendet. Progression führt dafür keine eigene
/// Identitätsart ein. Level, Rewards und externe Kommunikation sind noch nicht Teil
/// dieser Foundation.
/// </remarks>
public sealed class CommunityProgression
{
    private CommunityProgression(CommunityIdentityId communityIdentityId)
    {
        CommunityIdentityId = communityIdentityId;
        ExperiencePoints = ExperiencePoints.Zero;
    }

    /// <summary>
    /// Liefert die unveränderliche interne Identität, zu der dieser Zustand gehört.
    /// </summary>
    public CommunityIdentityId CommunityIdentityId { get; }

    /// <summary>
    /// Liefert den aktuell angesammelten Experience-Points-Wert.
    /// </summary>
    public ExperiencePoints ExperiencePoints { get; private set; }

    /// <summary>
    /// Erzeugt einen Progressionszustand mit null Experience Points.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <returns>Ein neuer Progressionszustand für die angegebene Identität.</returns>
    /// <exception cref="ArgumentException">Wenn <paramref name="communityIdentityId"/> leer ist.</exception>
    public static CommunityProgression Create(CommunityIdentityId communityIdentityId)
    {
        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Community-Progression benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }

        return new CommunityProgression(communityIdentityId);
    }

    /// <summary>
    /// Fügt dem Progressionszustand positive Experience Points hinzu.
    /// </summary>
    /// <param name="amount">Die zu vergebende positive Experience-Points-Menge.</param>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="amount"/> nicht positiv ist.</exception>
    /// <exception cref="OverflowException">Wenn die Addition <see cref="long.MaxValue"/> überschreiten würde.</exception>
    public void GrantExperience(long amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Eine Experience-Vergabe muss einen positiven Betrag enthalten.");
        }

        ExperiencePoints = ExperiencePoints.Add(amount);
    }
}
