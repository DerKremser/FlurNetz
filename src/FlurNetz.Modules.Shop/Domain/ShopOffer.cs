using FlurNetz.Modules.Inventory.Contracts;
using FlurNetz.Modules.Shop.Contracts;

namespace FlurNetz.Modules.Shop.Domain;

/// <summary>
/// Beschreibt ein fachliches Angebot des Shops.
/// </summary>
/// <remarks>
/// Das Modell enthält ausschließlich Angebotsstammdaten. Es kennt weder Käufe, Identitäten,
/// Economy, Persistence noch eine Bestandsvergabe. Neue Angebote sind immer deaktiviert.
/// </remarks>
public sealed class ShopOffer
{
    /// <summary>
    /// Maximale Länge des kanonischen Anzeigenamens in .NET-<see cref="string.Length"/>-Zeichen.
    /// </summary>
    public const int MaxDisplayNameLength = 200;

    /// <summary>
    /// Maximale Länge der kanonischen Beschreibung in .NET-<see cref="string.Length"/>-Zeichen.
    /// </summary>
    public const int MaxDescriptionLength = 2000;

    private ShopOffer(
        ShopOfferId id,
        ItemDefinitionId itemDefinitionId,
        string displayName,
        string? description,
        ShopPrice price,
        AvailabilityWindow availabilityWindow,
        int? purchaseLimitPerIdentity)
    {
        Id = id;
        ItemDefinitionId = itemDefinitionId;
        DisplayName = displayName;
        Description = description;
        Price = price;
        Availability = availabilityWindow;
        PurchaseLimitPerIdentity = purchaseLimitPerIdentity;
        IsEnabled = false;
    }

    /// <summary>
    /// Liefert die stabile fachliche Kennung des Angebots.
    /// </summary>
    public ShopOfferId Id { get; }

    /// <summary>
    /// Liefert die stabile fachliche Kennung des Angebots.
    /// </summary>
    public ShopOfferId ShopOfferId => Id;

    /// <summary>
    /// Liefert die unveränderliche Ziel-Item-Definition des Angebots.
    /// </summary>
    public ItemDefinitionId ItemDefinitionId { get; }

    /// <summary>
    /// Liefert den kanonisch getrimmten Anzeigenamen.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Liefert die kanonisch getrimmte Beschreibung oder <see langword="null"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Liefert den nicht-negativen Preis des Angebots.
    /// </summary>
    public ShopPrice Price { get; private set; }

    /// <summary>
    /// Liefert den Preis des Angebots.
    /// </summary>
    public ShopPrice ShopPrice => Price;

    /// <summary>
    /// Liefert, ob das Angebot fachlich aktiviert ist.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Liefert das Verfügbarkeitsfenster des Angebots.
    /// </summary>
    public AvailabilityWindow Availability { get; private set; }

    /// <summary>
    /// Liefert das Verfügbarkeitsfenster des Angebots.
    /// </summary>
    public AvailabilityWindow AvailabilityWindow => Availability;

    /// <summary>
    /// Liefert das optionale Kauflimit pro Identität.
    /// </summary>
    public int? PurchaseLimitPerIdentity { get; private set; }

    /// <summary>
    /// Erstellt ein neues, zunächst deaktiviertes Shop-Angebot.
    /// </summary>
    /// <param name="id">Die nicht leere stabile Angebots-ID.</param>
    /// <param name="itemDefinitionId">Das unveränderliche Ziel-Item.</param>
    /// <param name="displayName">Der Anzeigename.</param>
    /// <param name="description">Die optionale Beschreibung.</param>
    /// <param name="price">Der nicht-negative Preis.</param>
    /// <param name="availabilityWindow">Das optionale Verfügbarkeitsfenster.</param>
    /// <param name="purchaseLimitPerIdentity">Das optionale positive Kauflimit.</param>
    /// <returns>Ein gültiges und deaktiviertes Shop-Angebot.</returns>
    public static ShopOffer Create(
        ShopOfferId id,
        ItemDefinitionId itemDefinitionId,
        string displayName,
        string? description = null,
        ShopPrice price = default,
        AvailabilityWindow availabilityWindow = default,
        int? purchaseLimitPerIdentity = null)
    {
        EnsureValidId(id);
        EnsureValidItemDefinitionId(itemDefinitionId);

        return new ShopOffer(
            id,
            itemDefinitionId,
            NormalizeDisplayName(displayName),
            NormalizeDescription(description),
            price,
            availabilityWindow,
            NormalizePurchaseLimit(purchaseLimitPerIdentity));
    }

    /// <summary>
    /// Erstellt ein neues Angebot ohne Beschreibung.
    /// </summary>
    public static ShopOffer Create(
        ShopOfferId id,
        ItemDefinitionId itemDefinitionId,
        string displayName,
        ShopPrice price,
        AvailabilityWindow availabilityWindow = default,
        int? purchaseLimitPerIdentity = null)
    {
        return Create(
            id,
            itemDefinitionId,
            displayName,
            null,
            price,
            availabilityWindow,
            purchaseLimitPerIdentity);
    }

    /// <summary>
    /// Prüft die zeitliche Verfügbarkeit dieses Angebots zu einem übergebenen Zeitpunkt.
    /// </summary>
    public bool IsAvailableAt(DateTimeOffset at) => Availability.IsAvailableAt(at);

    /// <summary>
    /// Ändert den Anzeigenamen, sofern sich seine kanonische Form ändert.
    /// </summary>
    public bool Rename(string displayName)
    {
        var normalizedDisplayName = NormalizeDisplayName(displayName);
        if (string.Equals(DisplayName, normalizedDisplayName, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayName = normalizedDisplayName;
        return true;
    }

    /// <summary>
    /// Ändert den Anzeigenamen, sofern sich seine kanonische Form ändert.
    /// </summary>
    public bool ChangeDisplayName(string displayName) => Rename(displayName);

    /// <summary>
    /// Ändert oder entfernt die Beschreibung, sofern sich ihre kanonische Form ändert.
    /// </summary>
    public bool ChangeDescription(string? description)
    {
        var normalizedDescription = NormalizeDescription(description);
        if (string.Equals(Description, normalizedDescription, StringComparison.Ordinal))
        {
            return false;
        }

        Description = normalizedDescription;
        return true;
    }

    /// <summary>
    /// Ändert den Preis, sofern er sich ändert.
    /// </summary>
    public bool ChangePrice(ShopPrice price)
    {
        if (Price == price)
        {
            return false;
        }

        Price = price;
        return true;
    }

    /// <summary>
    /// Erstellt und ändert den Preis aus einem nicht-negativen Betrag.
    /// </summary>
    public bool ChangePrice(long value) => ChangePrice(ShopPrice.Create(value));

    /// <summary>
    /// Ändert das Verfügbarkeitsfenster, sofern es sich ändert.
    /// </summary>
    public bool ChangeAvailability(AvailabilityWindow availabilityWindow)
    {
        if (Availability == availabilityWindow)
        {
            return false;
        }

        Availability = availabilityWindow;
        return true;
    }

    /// <summary>
    /// Ändert oder entfernt das Kauflimit pro Identität.
    /// </summary>
    public bool ChangePurchaseLimit(int? purchaseLimitPerIdentity)
    {
        var normalizedLimit = NormalizePurchaseLimit(purchaseLimitPerIdentity);
        if (PurchaseLimitPerIdentity == normalizedLimit)
        {
            return false;
        }

        PurchaseLimitPerIdentity = normalizedLimit;
        return true;
    }

    /// <summary>
    /// Aktiviert das Angebot.
    /// </summary>
    public bool Enable()
    {
        if (IsEnabled)
        {
            return false;
        }

        IsEnabled = true;
        return true;
    }

    /// <summary>
    /// Deaktiviert das Angebot.
    /// </summary>
    public bool Disable()
    {
        if (!IsEnabled)
        {
            return false;
        }

        IsEnabled = false;
        return true;
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Der Shop-Anzeigename darf nicht leer oder aus Whitespace bestehen.",
                nameof(displayName));
        }

        var normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length > MaxDisplayNameLength)
        {
            throw new ArgumentException(
                "Der Shop-Anzeigename darf höchstens " + MaxDisplayNameLength + " Zeichen lang sein.",
                nameof(displayName));
        }

        return normalizedDisplayName;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Die Shop-Beschreibung darf nicht leer oder aus Whitespace bestehen.",
                nameof(description));
        }

        var normalizedDescription = description.Trim();
        if (normalizedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                "Die Shop-Beschreibung darf höchstens " + MaxDescriptionLength + " Zeichen lang sein.",
                nameof(description));
        }

        return normalizedDescription;
    }

    private static int? NormalizePurchaseLimit(int? purchaseLimitPerIdentity)
    {
        if (purchaseLimitPerIdentity is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purchaseLimitPerIdentity),
                purchaseLimitPerIdentity,
                "Das Kauflimit pro Identität muss größer als null sein.");
        }

        return purchaseLimitPerIdentity;
    }

    private static void EnsureValidId(ShopOfferId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Shop-Angebot benötigt eine nicht leere Shop-Angebots-ID.",
                nameof(id));
        }
    }

    private static void EnsureValidItemDefinitionId(ItemDefinitionId itemDefinitionId)
    {
        if (itemDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Ein Shop-Angebot benötigt eine nicht leere Item-Definition-ID.",
                nameof(itemDefinitionId));
        }
    }
}
