using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterExpenseRequest(
    Guid CategoryId,
    PositiveMoney Amount,
    DateOnly OperationDate,
    string? Description = null);
