using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed class InvestmentPortfolio
{
    public InvestmentPortfolio(Guid id, string name, Currency referenceCurrency)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo del portafoglio non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome del portafoglio è obbligatorio.", nameof(name));
        }

        if (referenceCurrency.Code is null)
        {
            throw new ArgumentException("La valuta di riferimento del portafoglio è obbligatoria.", nameof(referenceCurrency));
        }

        Id = id;
        Name = name;
        ReferenceCurrency = referenceCurrency;
    }

    public Guid Id { get; }

    public string Name { get; }

    public Currency ReferenceCurrency { get; }
}
