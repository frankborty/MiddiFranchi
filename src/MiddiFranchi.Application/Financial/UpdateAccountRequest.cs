using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

/// <summary>Modifica delle impostazioni di un conto esistente.</summary>
public sealed record UpdateAccountRequest(
    Guid AccountId,
    string Name,
    AccountType Type,
    AccountStatus Status,
    string? ColorHex,
    string? IconDataUrl);
