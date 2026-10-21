using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record NewManualBalance(Guid AccountId, Money Amount, DateOnly ValidAt);
