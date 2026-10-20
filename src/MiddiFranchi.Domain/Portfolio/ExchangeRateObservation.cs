using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record ExchangeRateObservation
{
    public ExchangeRateObservation(
        Guid id,
        ExchangeRate rate,
        DateOnly validAt,
        DateTimeOffset observedAt,
        string source)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo dell'osservazione di cambio non può essere vuoto.", nameof(id));
        }

        if (rate.Currency.Code is null)
        {
            throw new ArgumentException("Il cambio dell'osservazione è obbligatorio.", nameof(rate));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("La fonte dell'osservazione di cambio è obbligatoria.", nameof(source));
        }

        Id = id;
        Rate = rate;
        ValidAt = validAt;
        ObservedAt = observedAt;
        Source = source;
    }

    public Guid Id { get; }

    public ExchangeRate Rate { get; }

    public DateOnly ValidAt { get; }

    public DateTimeOffset ObservedAt { get; }

    public string Source { get; }
}
