namespace MiddiFranchi.Application.Financial;

public sealed record UpdateInvestmentOperationRequest(
    Guid OperationId,
    Guid SettlementAccountId,
    DateOnly OperationDate,
    decimal? Quantity = null,
    decimal? Price = null,
    decimal? FeeAmount = null,
    decimal? Amount = null,
    string? Description = null);
