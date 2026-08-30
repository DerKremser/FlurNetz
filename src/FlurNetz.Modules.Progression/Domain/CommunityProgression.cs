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
    private CommunityProgression(
        CommunityIdentityId communityIdentityId,
        ExperiencePoints experiencePoints)
    {
        CommunityIdentityId = communityIdentityId;
        ExperiencePoints = experiencePoints;
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
        EnsureValidCommunityIdentityId(communityIdentityId);

        return new CommunityProgression(communityIdentityId, ExperiencePoints.Zero);
    }

    /// <summary>
    /// Rekonstruiert einen bereits gespeicherten Progressionszustand.
    /// </summary>
    /// <param name="communityIdentityId">Die gültige interne Community-Identity-ID.</param>
    /// <param name="experiencePoints">Der bereits gespeicherte, nicht-negative XP-Wert.</param>
    /// <returns>Der rekonstruierte Progressionszustand.</returns>
    /// <remarks>
    /// <see cref="Create(CommunityIdentityId)"/> bedeutet fachlich einen neuen Zustand bei
    /// null XP. Diese Methode bedeutet dagegen ausschließlich die Rekonstruktion eines
    /// bereits vorhandenen Zustands für die Persistence-Schicht.
    /// </remarks>
    /// <exception cref="ArgumentException">Wenn <paramref name="communityIdentityId"/> leer ist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Wenn <paramref name="experiencePoints"/> ungültig ist.</exception>
    public static CommunityProgression Rehydrate(
        CommunityIdentityId communityIdentityId,
        ExperiencePoints experiencePoints)
    {
        EnsureValidCommunityIdentityId(communityIdentityId);

        return new CommunityProgression(
            communityIdentityId,
            ExperiencePoints.Create(experiencePoints.Value));
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

    private static void EnsureValidCommunityIdentityId(CommunityIdentityId communityIdentityId)
    {
        if (communityIdentityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine Community-Progression benötigt eine nicht leere Community-Identity-ID.",
                nameof(communityIdentityId));
        }
    }
}
