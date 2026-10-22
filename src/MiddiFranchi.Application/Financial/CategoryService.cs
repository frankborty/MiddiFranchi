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

        var updated = new Category(
            existing.Id,
            request.Name,
            request.Type,
            request.Description,
            request.Subcategory,
            request.Icon,
            request.ColorHex);
        await _categories.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _categories.GetByIdAsync(categoryId, cancellationToken);

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
        _categories.ListAsync(cancellationToken);
}
