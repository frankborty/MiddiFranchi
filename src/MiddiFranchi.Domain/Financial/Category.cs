namespace MiddiFranchi.Domain.Financial;

public sealed class Category
{
    public Category(Guid id, string name, CategoryType type, string? description = null, string? subcategory = null)
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
    }

    public Guid Id { get; }

    public string Name { get; }

    public CategoryType Type { get; }

    public string? Description { get; }

    public string? Subcategory { get; }
}
