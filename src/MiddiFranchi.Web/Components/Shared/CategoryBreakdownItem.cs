using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Web.Components.Shared;

/// <summary>Fetta del grafico a torta: etichetta, importo, quota, colore e offset cumulato.</summary>
public sealed record CategoryBreakdownItem(
    string Label,
    Money Amount,
    decimal Share,
    string Color,
    decimal Offset);
