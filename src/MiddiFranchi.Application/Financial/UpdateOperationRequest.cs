using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record UpdateOperationRequest(
    Guid OperationId,
    Guid CategoryId,
    PositiveMoney Amount,
    DateOnly OperationDate,
    string? Description = null);
