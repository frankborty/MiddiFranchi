using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterInvestmentOperationRequest(
    Guid InstrumentId,
    InvestmentOperationKind Kind,
    DateOnly OperationDate,
    decimal? Quantity = null,
    decimal? Price = null,
    decimal? FeeAmount = null,
    decimal? Amount = null,
    string? Description = null,
    decimal? TaxAmount = null);
