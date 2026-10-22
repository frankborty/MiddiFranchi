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

    public static string InstrumentTypeLabel(InstrumentType type) => type switch
    {
        InstrumentType.Share => "Azione",
        InstrumentType.Etf => "ETF",
        InstrumentType.Fund => "Fondo",
        InstrumentType.Bond => "Obbligazione",
        InstrumentType.Other => "Altro",
        _ => type.ToString(),
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
