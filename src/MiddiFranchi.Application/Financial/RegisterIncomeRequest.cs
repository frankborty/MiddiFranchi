using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterIncomeRequest(
    Guid CategoryId,
    PositiveMoney Amount,
    DateOnly OperationDate,
    string? Description = null);
