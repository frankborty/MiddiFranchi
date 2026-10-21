using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed record NewCategory(string Name, CategoryType Type, string? Description = null);
