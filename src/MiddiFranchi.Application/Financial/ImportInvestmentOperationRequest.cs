namespace MiddiFranchi.Application.Financial;

public sealed record ImportInvestmentOperationRequest(
    Guid OperationId,
    DateTimeOffset RecordedAt,
    RegisterInvestmentOperationRequest Operation);
