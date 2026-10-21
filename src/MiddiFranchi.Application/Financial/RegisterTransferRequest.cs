using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterTransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    PositiveMoney SourceAmount,
    PositiveMoney DestinationAmount,
    DateOnly OperationDate,
    string? Description = null,
    string? Notes = null);
