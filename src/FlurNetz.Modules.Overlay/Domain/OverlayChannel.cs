using FlurNetz.Modules.Overlay.Contracts;

namespace FlurNetz.Modules.Overlay.Domain;

/// <summary>Persistiertes Overlay-V1-Aggregat.</summary>
public sealed class OverlayChannel
{
    /// <summary>Maximale Anzeigenamenlänge in Unicode-Skalarwerten.</summary>
    public const int MaxDisplayNameLength = 100;

    /// <summary>Maximale Beschreibungslänge in Unicode-Skalarwerten.</summary>
    public const int MaxDescriptionLength = 500;

    private OverlayChannel(
        OverlayChannelId id,
        string displayName,
        string? description,
        bool isEnabled,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        IsEnabled = isEnabled;
        IsArchived = isArchived;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Stabile Kanal-ID.</summary>
    public OverlayChannelId Id { get; }

    /// <summary>Alias für explizite Aufrufer.</summary>
    public OverlayChannelId OverlayChannelId => Id;

    /// <summary>Alias für Management-Aufrufer.</summary>
    public OverlayChannelId ChannelId => Id;

    /// <summary>Kanonischer Anzeigename.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Kanonische optionale Beschreibung.</summary>
    public string? Description { get; private set; }

    /// <summary>Gibt an, ob der Kanal normale Automation-Alerts annimmt.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Gibt an, ob der Kanal terminal archiviert ist.</summary>
    public bool IsArchived { get; private set; }

    /// <summary>Erstellungszeitpunkt.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Zeitpunkt der letzten tatsächlichen Mutation.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Erstellt einen deaktivierten, nicht archivierten Kanal.</summary>
    public static OverlayChannel Create(
        OverlayChannelId id,
        string displayName,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        EnsureId(id);
        var created = OverlayTimestamp.Ensure(createdAtUtc, nameof(createdAtUtc));
        return new OverlayChannel(
            id,
            OverlayText.Required(displayName, nameof(displayName), "Der Overlay-Anzeigename", MaxDisplayNameLength),
            OverlayText.Optional(description, nameof(description), "Die Overlay-Beschreibung", MaxDescriptionLength),
            isEnabled: false,
            isArchived: false,
            created,
            created);
    }

    /// <summary>Rehydriert einen persistierten Kanal ohne beschädigte Werte zu reparieren.</summary>
    public static OverlayChannel Rehydrate(
        OverlayChannelId id,
        string displayName,
        string? description,
        bool isEnabled,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureId(id);
        OverlayText.EnsureCanonical(displayName, nameof(displayName), "der Overlay-Anzeigename", MaxDisplayNameLength, false);
        OverlayText.EnsureCanonical(description, nameof(description), "die Overlay-Beschreibung", MaxDescriptionLength, true);
        if (isArchived && isEnabled)
        {
            throw new ArgumentException("Ein archivierter Overlay-Kanal darf nicht aktiviert sein.", nameof(isEnabled));
        }

        var created = OverlayTimestamp.Ensure(createdAtUtc, nameof(createdAtUtc));
        var updated = OverlayTimestamp.Ensure(updatedAtUtc, nameof(updatedAtUtc));
        if (updated < created)
        {
            throw new ArgumentException("UpdatedAtUtc darf nicht vor CreatedAtUtc liegen.", nameof(updatedAtUtc));
        }

        return new OverlayChannel(id, displayName, description, isEnabled, isArchived, created, updated);
    }

    /// <summary>Ändert den Namen; ein identischer Wert ist ein No-op.</summary>
    public bool Rename(string displayName, DateTimeOffset updatedAtUtc) =>
        UpdateMetadata(displayName, Description, updatedAtUtc);

    /// <summary>Ändert die Beschreibung; ein identischer Wert ist ein No-op.</summary>
    public bool ChangeDescription(string? description, DateTimeOffset updatedAtUtc) =>
        UpdateMetadata(DisplayName, description, updatedAtUtc);

    /// <summary>Ändert Name und Beschreibung ohne archivierte Kanäle zu öffnen.</summary>
    public bool UpdateMetadata(string displayName, string? description, DateTimeOffset updatedAtUtc)
    {
        EnsureNotArchived();
        var normalizedName = OverlayText.Required(displayName, nameof(displayName), "Der Overlay-Anzeigename", MaxDisplayNameLength);
        var normalizedDescription = OverlayText.Optional(description, nameof(description), "Die Overlay-Beschreibung", MaxDescriptionLength);
        if (string.Equals(DisplayName, normalizedName, StringComparison.Ordinal)
            && string.Equals(Description, normalizedDescription, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayName = normalizedName;
        Description = normalizedDescription;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Aktiviert den Kanal idempotent.</summary>
    public bool Enable(DateTimeOffset updatedAtUtc)
    {
        EnsureNotArchived();
        if (IsEnabled) return false;
        IsEnabled = true;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Deaktiviert den Kanal idempotent.</summary>
    public bool Disable(DateTimeOffset updatedAtUtc)
    {
        if (IsArchived || !IsEnabled) return false;
        IsEnabled = false;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    /// <summary>Archiviert terminal und deaktiviert den Kanal.</summary>
    public bool Archive(DateTimeOffset updatedAtUtc)
    {
        if (IsArchived) return false;
        IsArchived = true;
        IsEnabled = false;
        SetUpdatedAt(updatedAtUtc);
        return true;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived) throw new OverlayChannelArchivedException(Id);
    }

    private void SetUpdatedAt(DateTimeOffset value)
    {
        var timestamp = OverlayTimestamp.Ensure(value, nameof(value));
        if (timestamp < CreatedAtUtc)
        {
            throw new ArgumentException("UpdatedAtUtc darf nicht vor CreatedAtUtc liegen.", nameof(value));
        }

        UpdatedAtUtc = timestamp;
    }

    private static void EnsureId(OverlayChannelId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Ein Overlay-Kanal benötigt eine nicht leere ID.", nameof(id));
        }
    }
}
