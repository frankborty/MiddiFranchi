using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record UpdateInvestmentAssetRequest(
    Guid InstrumentId,
    string Name,
    AssetClass AssetClass,
    PriceUnit PriceUnit,
    Money? NotionalValue = null,
    string? Symbol = null,
    string? Isin = null,
    string? ReferenceUrl = null,
    string? Notes = null,
    DateOnly? MaturityDate = null,
    decimal? CouponRate = null);
