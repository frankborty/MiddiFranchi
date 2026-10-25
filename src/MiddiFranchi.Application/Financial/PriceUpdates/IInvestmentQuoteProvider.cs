namespace MiddiFranchi.Application.Financial;

public interface IInvestmentQuoteProvider
{
    Task<InvestmentQuote> GetQuoteAsync(
        string isin,
        string exchange,
        CancellationToken cancellationToken = default);
}
