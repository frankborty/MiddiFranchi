namespace MiddiFranchi.Web.Components;

/// <summary>Icone e colori selezionabili per le categorie, con fallback neutro.</summary>
public static class CategoryAppearance
{
    public const string DefaultIcon = "tag";
    public const string DefaultColor = "#64748b";

    public static readonly IReadOnlyList<CategoryIconOption> Icons =
    [
        new("tag", "Generica"),
        new("home", "Casa"),
        new("bolt", "Bollette"),
        new("bus", "Trasporti"),
        new("car", "Auto"),
        new("plane", "Viaggi"),
        new("receipt", "Commissioni e ricevute"),
        new("shopping-bag", "Shopping"),
        new("heart-pulse", "Salute"),
        new("utensils", "Alimentazione"),
        new("coffee", "Caffè e bar"),
        new("tv", "Streaming e TV"),
        new("gift", "Regali e varie"),
        new("paw", "Animali"),
        new("wallet", "Portafoglio"),
        new("cash", "Contanti"),
        new("bank", "Banca"),
        new("transfer", "Trasferimenti"),
        new("chart", "Investimenti"),
        new("coin", "Risparmi"),
        new("loan", "Prestiti"),
        new("doc", "Documenti"),
        new("briefcase", "Lavoro"),
        new("trophy", "Premi"),
        new("calendar", "Calendario"),
        new("clock", "Tempo"),
        new("settings", "Altro"),
        new("cart", "Carrello"),
        new("shirt", "Abbigliamento"),
        new("baby", "Bambini"),
        new("book", "Libri"),
        new("music", "Musica"),
        new("gamepad", "Giochi"),
        new("phone", "Telefono"),
        new("laptop", "Informatica"),
        new("tools", "Attrezzi"),
        new("paint", "Ristrutturazione"),
        new("hammer", "Lavori manuali"),
        new("wrench", "Manutenzione"),
        new("brush", "Pulizia"),
        new("fuel", "Carburante"),
        new("parking", "Parcheggio"),
        new("ticket", "Biglietti"),
        new("film", "Cinema"),
        new("dumbbell", "Sport"),
        new("bike", "Bicicletta"),
        new("train", "Treno"),
        new("ship", "Navi"),
        new("luggage", "Bagagli"),
        new("map-pin", "Luoghi"),
        new("globe", "Viaggi internazionali"),
        new("glasses", "Occhiali"),
        new("scissors", "Cura personale"),
        new("sofa", "Arredamento"),
        new("bed", "Camera"),
        new("lamp", "Illuminazione"),
        new("washer", "Elettrodomestici"),
        new("plug", "Utenze"),
        new("droplet", "Acqua"),
        new("flame", "Gas e riscaldamento"),
        new("wifi", "Internet"),
        new("recycle", "Rifiuti"),
        new("stethoscope", "Medico"),
        new("pill", "Farmaci"),
        new("hospital", "Ospedale"),
        new("heart", "Donazioni"),
        new("leaf", "Natura"),
        new("flower", "Piante"),
        new("apple", "Frutta e verdura"),
        new("pizza", "Ristorante"),
        new("cake", "Dolci"),
        new("wine", "Vino"),
        new("beer", "Birra"),
        new("users", "Famiglia"),
        new("graduation-cap", "Istruzione"),
        new("shield", "Assicurazioni"),
        new("lock", "Sicurezza"),
        new("key", "Chiavi"),
        new("credit-card", "Carte di credito"),
        new("piggy-bank", "Salvadanaio"),
        new("percent", "Tasse e percentuali"),
        new("calculator", "Contabilità"),
        new("folder", "Archivio"),
        new("mail", "Posta"),
        new("package", "Spedizioni"),
        new("camera", "Fotografia"),
        new("palette", "Hobby creativi"),
        new("puzzle", "Passatempi"),
        new("bell", "Avvisi"),
        new("star", "Preferiti"),
        new("target", "Obiettivi"),
        new("rocket", "Progetti"),
        new("bulb", "Idee"),
        new("umbrella", "Protezione"),
        new("mountain", "Vacanze"),
    ];

    public static string IconFor(string? icon) =>
        string.IsNullOrWhiteSpace(icon) ? DefaultIcon : icon.Trim();

    public static string ColorFor(string? colorHex) =>
        IsHexColor(colorHex) ? colorHex!.Trim() : DefaultColor;

    public static string LabelFor(string? icon) =>
        Icons.FirstOrDefault(option => option.Name == IconFor(icon))?.Label ?? "Generica";

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);
}

public sealed record CategoryIconOption(string Name, string Label);
