using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed record NewAccount(string Name, AccountType Type);
