using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Operazioni amministrative sulle categorie: gruppi categoria/sottocategoria,
/// rinomina, spostamento delle operazioni e cancellazione sicura.
/// </summary>
public sealed class CategoryAdminService
{
    private readonly ICategoryRepository _categories;
    private readonly IFinancialOperationRepository _operations;

    public CategoryAdminService(
        ICategoryRepository categories,
        IFinancialOperationRepository operations)
    {
        _categories = categories;
        _operations = operations;
    }

    public async Task<Category> CreateCategoryAsync(
        NewCategory request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della categoria è obbligatorio.", nameof(request));
        }

        if (request.Type == CategoryType.Expense && string.IsNullOrWhiteSpace(request.Subcategory))
        {
            throw new ArgumentException("Inserisci la prima sottocategoria della categoria di uscita.", nameof(request));
        }

        var categories = await _categories.ListAsync(cancellationToken);
        if (categories.Any(category =>
                category.Type == request.Type &&
                Same(category.Name, name)))
        {
            throw new InvalidOperationException("Esiste già una categoria con questo nome per lo stesso tipo.");
        }

        var created = new Category(
            Guid.NewGuid(),
            name,
            request.Type,
            request.Description,
            request.Type == CategoryType.Expense ? request.Subcategory!.Trim() : null,
            request.Icon,
            request.ColorHex);
        await _categories.AddAsync(created, cancellationToken);
        return created;
    }

    public async Task<Category> AddSubcategoryAsync(
        Guid categoryId,
        string subcategory,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var source = await _categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Categoria non trovata.");

        if (source.Type != CategoryType.Expense)
        {
            throw new InvalidOperationException("Le categorie di entrata non hanno sottocategorie.");
        }

        var name = subcategory.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della sottocategoria è obbligatorio.", nameof(subcategory));
        }

        var categories = await _categories.ListAsync(cancellationToken);
        var group = categories
            .Where(category => category.Type == source.Type && Same(category.Name, source.Name))
            .ToArray();
        if (group.Any(category => Same(category.Subcategory, name)))
        {
            throw new InvalidOperationException("La sottocategoria esiste già in questa categoria.");
        }

        var appearance = CanonicalAppearance(group);
        var created = new Category(
            Guid.NewGuid(),
            source.Name,
            source.Type,
            string.IsNullOrWhiteSpace(description) ? source.Description : description.Trim(),
            name,
            appearance.Icon,
            appearance.ColorHex);
        await _categories.AddAsync(created, cancellationToken);
        return created;
    }

    public async Task UpdateGroupAsync(
        CategoryType type,
        string currentName,
        string newName,
        string? description,
        string? icon,
        string? colorHex,
        CancellationToken cancellationToken = default)
    {
        var name = newName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della categoria è obbligatorio.", nameof(newName));
        }

        var categories = await _categories.ListAsync(cancellationToken);
        var group = categories
            .Where(category => category.Type == type && Same(category.Name, currentName))
            .ToArray();
        if (group.Length == 0)
        {
            throw new InvalidOperationException("Categoria non trovata.");
        }

        if (!Same(currentName, name) &&
            categories.Any(category => category.Type == type && Same(category.Name, name)))
        {
            throw new InvalidOperationException("Esiste già una categoria con questo nome per lo stesso tipo.");
        }

        foreach (var category in group)
        {
            var updated = new Category(
                category.Id,
                name,
                category.Type,
                description,
                category.Subcategory,
                icon,
                colorHex);
            await _categories.UpdateAsync(updated, cancellationToken);
        }
    }

    public async Task UpdateSubcategoryAsync(
        Guid categoryId,
        string subcategory,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var source = await _categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException("Sottocategoria non trovata.");

        if (source.Type != CategoryType.Expense)
        {
            throw new InvalidOperationException("Le categorie di entrata non hanno sottocategorie.");
        }

        var name = subcategory.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della sottocategoria è obbligatorio.", nameof(subcategory));
        }

        var categories = await _categories.ListAsync(cancellationToken);
        var group = categories
            .Where(category => category.Type == source.Type && Same(category.Name, source.Name))
            .ToArray();
        if (group.Any(category =>
                category.Id != source.Id &&
                Same(category.Subcategory, name)))
        {
            throw new InvalidOperationException("La sottocategoria esiste già in questa categoria.");
        }

        var appearance = CanonicalAppearance(group);
        var updated = new Category(
            source.Id,
            source.Name,
            source.Type,
            description,
            name,
            appearance.Icon,
            appearance.ColorHex);
        await _categories.UpdateAsync(updated, cancellationToken);
    }

    public async Task<int> MoveCategoryOperationsAsync(
        CategoryType type,
        string sourceName,
        Guid destinationCategoryId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);
        var sources = categories
            .Where(category => category.Type == type && Same(category.Name, sourceName))
            .ToArray();
        if (sources.Length == 0)
        {
            throw new InvalidOperationException("Categoria di origine non trovata.");
        }

        var destination = categories.SingleOrDefault(category => category.Id == destinationCategoryId)
            ?? throw new InvalidOperationException("Categoria di destinazione non trovata.");
        if (destination.Type != type)
        {
            throw new InvalidOperationException("La categoria di destinazione deve avere lo stesso tipo.");
        }

        if (sources.Any(category => category.Id == destination.Id))
        {
            throw new InvalidOperationException("Scegli una categoria di destinazione diversa da quella di origine.");
        }

        return await ReassignOperationsAsync(
            sources.Select(category => category.Id).ToArray(),
            destination,
            cancellationToken);
    }

    public async Task<int> MoveSubcategoryOperationsAsync(
        Guid sourceCategoryId,
        Guid destinationCategoryId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);
        var source = categories.SingleOrDefault(category => category.Id == sourceCategoryId)
            ?? throw new InvalidOperationException("Sottocategoria di origine non trovata.");
        var destination = categories.SingleOrDefault(category => category.Id == destinationCategoryId)
            ?? throw new InvalidOperationException("Sottocategoria di destinazione non trovata.");

        if (source.Type != destination.Type)
        {
            throw new InvalidOperationException("Le sottocategorie devono avere lo stesso tipo.");
        }

        if (source.Id == destination.Id)
        {
            throw new InvalidOperationException("Scegli una sottocategoria di destinazione diversa.");
        }

        return await ReassignOperationsAsync([source.Id], destination, cancellationToken);
    }

    public async Task<int> DeleteCategoryGroupAsync(
        CategoryType type,
        string name,
        Guid? destinationCategoryId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);
        var group = categories
            .Where(category => category.Type == type && Same(category.Name, name))
            .ToArray();
        if (group.Length == 0)
        {
            return 0;
        }

        var sourceIds = group.Select(category => category.Id).ToArray();
        var moved = 0;
        if (await CountOperationsAsync(sourceIds, cancellationToken) > 0)
        {
            if (destinationCategoryId is null)
            {
                throw new InvalidOperationException(
                    "La categoria ha operazioni collegate: scegli una categoria di destinazione prima di eliminarla.");
            }

            var destination = categories.SingleOrDefault(category => category.Id == destinationCategoryId.Value)
                ?? throw new InvalidOperationException("Categoria di destinazione non trovata.");
            if (destination.Type != type)
            {
                throw new InvalidOperationException("La categoria di destinazione deve avere lo stesso tipo.");
            }

            if (sourceIds.Contains(destination.Id))
            {
                throw new InvalidOperationException("La destinazione deve essere esterna alla categoria da eliminare.");
            }

            moved = await ReassignOperationsAsync(sourceIds, destination, cancellationToken);
        }

        foreach (var category in group)
        {
            await _categories.DeleteAsync(category.Id, cancellationToken);
        }

        return moved;
    }

    public async Task<int> DeleteSubcategoryAsync(
        Guid categoryId,
        Guid? destinationCategoryId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);
        var source = categories.SingleOrDefault(category => category.Id == categoryId)
            ?? throw new InvalidOperationException("Sottocategoria non trovata.");

        var moved = 0;
        if (await CountOperationsAsync([source.Id], cancellationToken) > 0)
        {
            if (destinationCategoryId is null)
            {
                throw new InvalidOperationException(
                    "La sottocategoria ha operazioni collegate: scegli una destinazione prima di eliminarla.");
            }

            var destination = categories.SingleOrDefault(category => category.Id == destinationCategoryId.Value)
                ?? throw new InvalidOperationException("Sottocategoria di destinazione non trovata.");
            if (destination.Type != source.Type)
            {
                throw new InvalidOperationException("La destinazione deve avere lo stesso tipo.");
            }

            if (destination.Id == source.Id)
            {
                throw new InvalidOperationException("Scegli una destinazione diversa da quella da eliminare.");
            }

            moved = await ReassignOperationsAsync([source.Id], destination, cancellationToken);
        }

        await _categories.DeleteAsync(source.Id, cancellationToken);
        return moved;
    }

    public async Task<int> CountOperationsAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        if (categoryIds.Count == 0)
        {
            return 0;
        }

        var operations = await _operations.ListAsync(cancellationToken);
        return operations.Count(operation =>
            CategoryIdOf(operation) is { } categoryId && categoryIds.Contains(categoryId));
    }

    private async Task<int> ReassignOperationsAsync(
        IReadOnlyCollection<Guid> sourceCategoryIds,
        Category destination,
        CancellationToken cancellationToken)
    {
        var sourceIds = sourceCategoryIds.ToHashSet();
        var operations = await _operations.ListAsync(cancellationToken);
        var moved = 0;
        foreach (var operation in operations)
        {
            if (CategoryIdOf(operation) is not { } currentCategoryId ||
                !sourceIds.Contains(currentCategoryId))
            {
                continue;
            }

            FinancialOperation updated = operation switch
            {
                Income income => new Income(
                    income.Id,
                    income.Account,
                    destination,
                    income.Amount,
                    income.OperationDate,
                    income.RecordedAt,
                    income.Description,
                    income.Notes),
                Expense expense => new Expense(
                    expense.Id,
                    expense.Account,
                    destination,
                    expense.Amount,
                    expense.OperationDate,
                    expense.RecordedAt,
                    expense.Description,
                    expense.Notes,
                    expense.Instrument),
                _ => throw new InvalidOperationException("Tipo di operazione non gestito."),
            };

            await _operations.UpdateAsync(updated, cancellationToken);
            moved++;
        }

        return moved;
    }

    private static Guid? CategoryIdOf(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Id,
        Expense expense => expense.Category.Id,
        _ => null,
    };

    /// <summary>Icona e colore condivisi dalla categoria, scelti in modo deterministico.</summary>
    private static (string? Icon, string? ColorHex) CanonicalAppearance(IEnumerable<Category> group) =>
        group
            .GroupBy(category => (category.Icon, category.ColorHex))
            .OrderByDescending(appearance => appearance.Count())
            .ThenBy(appearance => appearance.Key.Icon ?? "", StringComparer.Ordinal)
            .Select(appearance => appearance.Key)
            .First();

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.CurrentCultureIgnoreCase);
}
