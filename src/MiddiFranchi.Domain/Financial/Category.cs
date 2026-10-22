namespace MiddiFranchi.Domain.Financial;

public sealed class Category
{
    public Category(
        Guid id,
        string name,
        CategoryType type,
        string? description = null,
        string? subcategory = null,
        string? icon = null,
        string? colorHex = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo della categoria non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della categoria è obbligatorio.", nameof(name));
        }

        Id = id;
        Name = name;
        Type = type;
        Description = description;
        Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        ColorHex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim();
    }

    public Guid Id { get; }

    public string Name { get; }

    public CategoryType Type { get; }

    public string? Description { get; }

    public string? Subcategory { get; }

    /// <summary>Nome dell'icona MfIcon associata alla categoria.</summary>
    public string? Icon { get; }

    /// <summary>Colore esadecimale (#RRGGBB) della categoria.</summary>
    public string? ColorHex { get; }
}
