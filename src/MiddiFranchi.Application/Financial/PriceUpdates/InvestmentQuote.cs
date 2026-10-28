namespace MiddiFranchi.Application.Financial;

public sealed record InvestmentQuote(
    string Isin,
    decimal Price,
    string Currency,
    DateOnly? AsOf,
    string Source,
    TimeOnly? LastTradeTime = null);
