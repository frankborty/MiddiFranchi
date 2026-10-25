using System.Globalization;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Web.Components;

public enum OperationFormKind
{
    Income,
    Expense,
    Transfer,
}

public static class OperationForm
{
    public static CategoryType CategoryTypeFor(OperationFormKind kind) => kind switch
    {
        OperationFormKind.Income => CategoryType.Income,
        OperationFormKind.Expense => CategoryType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Il trasferimento non usa categorie."),
    };

    /// <summary>Filtro delle categorie compatibili col tipo di operazione scelto (il filtro UI non sostituisce la validazione dell'Application).</summary>
    public static IReadOnlyList<Category> CategoriesFor(
        OperationFormKind kind,
        IReadOnlyList<Category> categories) =>
        kind == OperationFormKind.Transfer
            ? []
            : categories.Where(category => category.Type == CategoryTypeFor(kind)).ToArray();

    /// <summary>Raggruppa le categorie per nome, ordinando le sottocategorie: il menu le mostra come optgroup.</summary>
    public static IReadOnlyList<CategoryGroup> GroupCategories(IReadOnlyList<Category> categories) =>
        categories
            .GroupBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new CategoryGroup(
                group.Key,
                group.OrderBy(category => category.Subcategory, StringComparer.CurrentCultureIgnoreCase).ToArray()))
            .ToArray();
}

public sealed record CategoryGroup(string Name, IReadOnlyList<Category> Categories);

/// <summary>
/// Stato del modulo di registrazione delle operazioni. Contiene solo dati di presentazione:
/// nessuna regola di dominio è duplicata, le convalide definitive restano nell'Application.
/// </summary>
public sealed record OperationFormModel
{
    public OperationFormKind Kind { get; init; } = OperationFormKind.Expense;

    public Guid? CategoryId { get; init; }

    public Guid? SourceAccountId { get; init; }

    public Guid? DestinationAccountId { get; init; }

    public string SourceAmountText { get; init; } = "";

    public DateOnly? ValidAt { get; init; }

    public string? Description { get; init; }
}

public sealed record OperationFormResult(
    bool Succeeded,
    string? SuccessMessage,
    string? Error)
{
    public static OperationFormResult Ok(string message) => new(true, message, null);

    public static OperationFormResult Fail(string error) => new(false, null, error);
}

public sealed class OperationFormDispatcher
{
    private static readonly CultureInfo[] AcceptedCultures =
    [
        CultureInfo.InvariantCulture,
        new CultureInfo("it-IT"),
    ];

    private readonly OperationRegistrationService _registrations;

    public OperationFormDispatcher(OperationRegistrationService registrations)
    {
        _registrations = registrations;
    }

    /// <summary>Registra un'entrata o un'uscita senza conto. I trasferimenti non sono esposti dalla UI.</summary>
    public Task<OperationFormResult> SubmitAsync(OperationFormModel model) =>
        SubmitAsync(model, new Dictionary<Guid, Account>());

    /// <summary>
    /// Inoltra la richiesta al servizio corretto in base al tipo scelto. Restituisce un
    /// esito strutturato: gli errori dell'Application vengono riproposti senza stack trace.
    /// </summary>
    public async Task<OperationFormResult> SubmitAsync(
        OperationFormModel model,
        IReadOnlyDictionary<Guid, Account> accountsById)
    {
        var missingField = ValidateRequiredFields(model);
        if (missingField is not null)
        {
            return OperationFormResult.Fail(missingField);
        }

        try
        {
            return model.Kind switch
            {
                OperationFormKind.Income => await SubmitIncomeAsync(model),
                OperationFormKind.Expense => await SubmitExpenseAsync(model),
                OperationFormKind.Transfer => await SubmitTransferAsync(model, accountsById),
                _ => OperationFormResult.Fail("Il tipo di operazione non è valido."),
            };
        }
        catch (InvalidOperationException exception)
        {
            return OperationFormResult.Fail(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return OperationFormResult.Fail(exception.Message);
        }
        catch (Exception)
        {
            return OperationFormResult.Fail("Registrazione non riuscita: verificare la connessione al database e riprovare.");
        }
    }

    private async Task<OperationFormResult> SubmitIncomeAsync(OperationFormModel model)
    {
        var amount = RequiredPositiveAmount(model.SourceAmountText, "importo");
        await _registrations.IncomeAsync(new RegisterIncomeRequest(
            model.CategoryId!.Value,
            new PositiveMoney(new Money(amount, Currency.Euro)),
            model.ValidAt!.Value,
            model.Description));
        return OperationFormResult.Ok($"Entrata di {Format.Money(new Money(amount, Currency.Euro))} registrata.");
    }

    private async Task<OperationFormResult> SubmitExpenseAsync(OperationFormModel model)
    {
        var amount = RequiredPositiveAmount(model.SourceAmountText, "importo");
        await _registrations.ExpenseAsync(new RegisterExpenseRequest(
            model.CategoryId!.Value,
            new PositiveMoney(new Money(amount, Currency.Euro)),
            model.ValidAt!.Value,
            model.Description));
        return OperationFormResult.Ok($"Uscita di {Format.Money(new Money(amount, Currency.Euro))} registrata.");
    }

    private async Task<OperationFormResult> SubmitTransferAsync(
        OperationFormModel model,
        IReadOnlyDictionary<Guid, Account> accountsById)
    {
        var source = RequiredAccount(accountsById, model.SourceAccountId, "conto di origine");
        var destination = RequiredAccount(accountsById, model.DestinationAccountId, "conto di destinazione");

        if (source.Id == destination.Id)
        {
            return OperationFormResult.Fail("Scegli due conti distinti: origine e destinazione coincidono.");
        }

        var amount = RequiredPositiveAmount(model.SourceAmountText, "importo");
        await _registrations.TransferAsync(new RegisterTransferRequest(
            source.Id,
            destination.Id,
            new PositiveMoney(new Money(amount, source.Currency)),
            new PositiveMoney(new Money(amount, destination.Currency)),
            model.ValidAt!.Value,
            model.Description));
        return OperationFormResult.Ok(
            $"Trasferimento di {Format.Money(new Money(amount, source.Currency))} da «{source.Name}» a «{destination.Name}» registrato.");
    }

    private static string? ValidateRequiredFields(OperationFormModel model)
    {
        if (model.ValidAt is null)
        {
            return "La data economica è obbligatoria.";
        }

        if (model.Kind is not OperationFormKind.Transfer && model.CategoryId is null)
        {
            return "La categoria è obbligatoria.";
        }

        return null;
    }

    private static Account RequiredAccount(
        IReadOnlyDictionary<Guid, Account> accountsById,
        Guid? accountId,
        string fieldName)
    {
        if (accountId is null)
        {
            throw new ArgumentException($"Seleziona il {fieldName}.");
        }

        return accountsById.TryGetValue(accountId.Value, out var account)
            ? account
            : throw new ArgumentException($"Il {fieldName} selezionato non esiste.");
    }

    private static decimal RequiredPositiveAmount(string? text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"Il {fieldName} è obbligatorio.");
        }

        if (!TryParseAmount(text, out var amount))
        {
            throw new ArgumentException($"Il {fieldName} deve essere un numero valido (per esempio: 123,45).");
        }

        if (amount <= 0m)
        {
            throw new ArgumentException($"Il {fieldName} deve essere un importo positivo.");
        }

        return amount;
    }

    private static bool TryParseAmount(string? text, out decimal amount) =>
        DecimalText.TryParse(text, out amount);
}
