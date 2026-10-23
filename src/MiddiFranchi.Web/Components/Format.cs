using System.Globalization;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Web.Components;

public static class Format
{
    private static readonly CultureInfo EuroCulture = new("it-IT");

    /// <summary>Importo formattato in EUR, senza conversioni.</summary>
    public static string Money(Money money) =>
        money.Amount.ToString("C", EuroCulture);

    public static string MoneySigned(Money money) =>
        money.Amount switch
        {
            > 0 => "+" + Money(money),
            < 0 => Money(money),
            _ => "0",
        };

    /// <summary>Importo senza decimali per le etichette degli assi; non è un valore finanziario arrotondato.</summary>
    public static string MoneyNoDecimals(Money money) =>
        money.Amount.ToString("C0", EuroCulture);

    public static string Date(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string ChartDate(DateOnly date, bool includeYear = false) =>
        date.ToString(includeYear ? "MMM yyyy" : "d MMM", CultureInfo.GetCultureInfo("it-IT"));

    /// <summary>Periodo leggibile in italiano, senza conversioni di data.</summary>
    public static string MonthPeriod(DateOnly start, DateOnly end)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        return start.Year == end.Year && start.Month == end.Month
            ? $"{start.Day}–{end.Day} {start.ToString("MMMM yyyy", culture)}"
            : $"{Date(start)} – {Date(end)}";
    }

    public static string UtcTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Importo o "Saldo non registrato" quando la rilevazione manca: mai zero per convenzione.</summary>
    public static string BalanceOrUnavailable(Money? balance) =>
        balance is null ? "Saldo non registrato" : Money(balance.Value);

    public static string AccountTypeLabel(AccountType type) => type switch
    {
        AccountType.Checking => "Conto corrente",
        AccountType.Card => "Carta di credito",
        AccountType.Cash => "Cassa",
        AccountType.Liability => "Passività monetaria",
        AccountType.Loans => "Prestiti",
        _ => type.ToString(),
    };

    public static string AccountStatusLabel(AccountStatus status) => status switch
    {
        AccountStatus.Active => "Attivo",
        AccountStatus.Closed => "Chiuso",
        _ => status.ToString(),
    };

    public static string CategoryTypeLabel(CategoryType type) => type switch
    {
        CategoryType.Income => "Entrate",
        CategoryType.Expense => "Uscite",
        _ => type.ToString(),
    };

    public static string OperationKindLabel(OperationKind kind) => kind switch
    {
        OperationKind.Income => "Entrata",
        OperationKind.Expense => "Uscita",
        OperationKind.Transfer => "Trasferimento",
        OperationKind.Investment => "Investimento",
        OperationKind.Reversal => "Storno",
        _ => kind.ToString(),
    };

    public static string InvestmentOperationKindLabel(InvestmentOperationKind kind) => kind switch
    {
        InvestmentOperationKind.Buy => "Acquisto",
        InvestmentOperationKind.Sell => "Vendita",
        InvestmentOperationKind.Dividend => "Dividendo",
        InvestmentOperationKind.Coupon => "Cedola",
        InvestmentOperationKind.StandaloneFee => "Commissione",
        InvestmentOperationKind.TransferIn => "Trasferimento in entrata",
        InvestmentOperationKind.TransferOut => "Trasferimento in uscita",
        InvestmentOperationKind.QuantityEvent => "Evento di quantità",
        _ => kind.ToString(),
    };

    public static string AssetClassLabel(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Equity => "Azionario",
        AssetClass.Bond => "Obbligazionario",
        AssetClass.MoneyMarket => "Monetario",
        AssetClass.RealEstate => "Immobiliare",
        AssetClass.Commodity => "Materie prime",
        AssetClass.Crypto => "Cripto",
        AssetClass.Mixed => "Bilanciato",
        AssetClass.Pension => "Previdenza",
        AssetClass.Other => "Altro",
        _ => assetClass.ToString(),
    };

    public static string InstrumentStatusLabel(InstrumentStatus status) => status switch
    {
        InstrumentStatus.Active => "Attivo",
        InstrumentStatus.Archived => "Archiviato",
        _ => status.ToString(),
    };

    public static string PriceUnitLabel(PriceUnit unit) => unit switch
    {
        PriceUnit.PerUnit => "Per unità",
        PriceUnit.PercentOfNominal => "% del nominale",
        _ => unit.ToString(),
    };

    public static string Quantity(Quantity quantity) =>
        quantity.Value.ToString("N8", CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.');

    public static string Percent(decimal ratio) =>
        ratio.ToString("P1", CultureInfo.GetCultureInfo("it-IT"));

    public static string SignedPercent(decimal ratio) =>
        ratio > 0m ? "+" + Percent(ratio) : Percent(ratio);
}
