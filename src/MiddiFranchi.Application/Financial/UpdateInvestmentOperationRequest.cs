namespace MiddiFranchi.Application.Financial;

public sealed record UpdateInvestmentOperationRequest(
    Guid OperationId,
    DateOnly OperationDate,
    decimal? Quantity = null,
    decimal? Price = null,
    decimal? FeeAmount = null,
    decimal? Amount = null,
    string? Description = null,
    decimal? TaxAmount = null);
