using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed class CategoryService
{
    private readonly ICategoryRepository _categories;

    public CategoryService(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<Category> CreateAsync(NewCategory request, CancellationToken cancellationToken = default)
    {
        var category = new Category(
            Guid.NewGuid(),
            request.Name,
            request.Type,
            request.Description,
            request.Subcategory,
            request.Icon,
            request.ColorHex);
        await _categories.AddAsync(category, cancellationToken);
        return category;
    }

    public async Task<Category> UpdateAsync(Guid categoryId, NewCategory request, CancellationToken cancellationToken = default)
    {
        var existing = await _categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Categoria non trovata.");

        // Nome, tipo, icona e colore appartengono alla categoria: le sottocategorie
        // condividono lo stesso aspetto. Si aggiornano quindi tutte le righe del gruppo.
        var all = await _categories.ListAsync(cancellationToken);
        var group = all
            .Where(category =>
                category.Type == existing.Type &&
                string.Equals(
                    category.Name.Trim(),
                    existing.Name.Trim(),
                    StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        Category? selected = null;
        foreach (var row in group)
        {
            var isSelected = row.Id == categoryId;
            var updated = new Category(
                row.Id,
                request.Name,
                request.Type,
                isSelected ? request.Description : row.Description,
                isSelected ? request.Subcategory : row.Subcategory,
                request.Icon,
                request.ColorHex);
            await _categories.UpdateAsync(updated, cancellationToken);
            if (isSelected)
            {
                selected = updated;
            }
        }

        return selected ?? throw new InvalidOperationException("Categoria non trovata.");
    }

    public Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _categories.GetByIdAsync(categoryId, cancellationToken);

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
        _categories.ListAsync(cancellationToken);
}
