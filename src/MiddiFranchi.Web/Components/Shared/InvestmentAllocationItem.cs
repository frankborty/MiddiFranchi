using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Web.Components.Shared;

public sealed record InvestmentAllocationItem(
    string Name,
    Money Value,
    decimal Share,
    decimal Offset,
    string Color,
    decimal? TotalResultPercent);
