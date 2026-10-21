using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterIncomeRequest(
    Guid AccountId,
    Guid CategoryId,
    PositiveMoney Amount,
    DateOnly OperationDate,
    string? Description = null,
    string? Notes = null);
