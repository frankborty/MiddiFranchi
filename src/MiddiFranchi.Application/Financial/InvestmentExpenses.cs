using MiddiFranchi.Application.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Le spese collegate a un'operazione d'investimento (tasse e commissioni) sono operazioni
/// separate. L'ID deriva dall'ID dell'operazione, così ogni operazione ha la propria spesa
/// anche quando più operazioni cadono nello stesso giorno.
/// </summary>
internal static class InvestmentExpenses
{
    private const string TaxScope = "tax-expense";
    private const string FeeScope = "fee-expense";

    public static Guid TaxIdForOperation(Guid operationId) =>
        DeterministicId.From(TaxScope, operationId.ToString("N"));

    public static Guid FeeIdForOperation(Guid operationId) =>
        DeterministicId.From(FeeScope, operationId.ToString("N"));
}
