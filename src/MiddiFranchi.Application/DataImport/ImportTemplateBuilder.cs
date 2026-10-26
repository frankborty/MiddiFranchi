using System.IO.Compression;
using System.Text;

namespace MiddiFranchi.Application.DataImport;

internal static class ImportTemplateBuilder
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static ImportTemplate BuildAccountsTemplate()
    {
        var csv = CsvDocument.Write(
            ["name", "type", "status", "color_hex", "icon", "loans"],
            [
                new string?[] { "Conto esempio", "Checking", "Active", "#2563eb", "wallet", "" },
                new string?[] { "Prestito esempio", "Loans", "Active", "", "", "Mario Rossi;Luigi Verdi" },
            ]);

        var content = new byte[Utf8Bom.Length + Encoding.UTF8.GetByteCount(csv)];
        Utf8Bom.CopyTo(content, 0);
        Encoding.UTF8.GetBytes(csv, content);

        return new ImportTemplate(
            "MiddiFranchi-import-conti.csv",
            content);
    }

    public static ImportTemplate Build()
    {
        var accounts = CsvDocument.Write(
            ["name", "type", "status", "color_hex", "icon", "loans"],
            [
                new string?[]
                    { "Conto esempio", "Checking", "Active", "#2563eb", "wallet", "" },
                new string?[]
                    { "Prestito esempio", "Loans", "Active", "", "", "Mario Rossi;Luigi Verdi" },
            ]);

        var balances = CsvDocument.Write(
            ["account_name", "date", "amount"],
            [
                new string?[] { "Conto esempio", "2024-01-01", "1000.00" },
            ]);

        var instruments = CsvDocument.Write(
            [
                "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate", "tax_rate",
            ],
            [
                new string?[]
                {
                    "ETF esempio", "Equity", "Active", "EXMP", "IT0000000001",
                    "EUR", "PerUnit", "", "", "", "", "", "", "26",
                },
            ]);

        var prices = CsvDocument.Write(
            [
                "instrument_isin", "instrument_symbol", "instrument_name",
                "date", "price", "currency", "source",
            ],
            [
                new string?[]
                {
                    "IT0000000001", "EXMP", "ETF esempio",
                    "2024-01-02", "100.50", "EUR", "Import",
                },
            ]);

        var operations = CsvDocument.Write(
            [
                "kind", "date", "instrument_isin", "instrument_symbol", "instrument_name",
                "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "Buy", "2024-01-03", "IT0000000001", "EXMP", "ETF esempio",
                    "10", "100.50", "2.50", "Acquisto iniziale",
                },
            ]);

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Accounts, accounts, addBom: true);
            WriteEntry(archive, ImportFileNames.AccountBalances, balances, addBom: true);
            WriteEntry(archive, ImportFileNames.Instruments, instruments, addBom: true);
            WriteEntry(archive, ImportFileNames.InstrumentPrices, prices, addBom: true);
            WriteEntry(archive, ImportFileNames.Operations, operations, addBom: true);
            WriteEntry(archive, ImportFileNames.Readme, Readme, addBom: false);
        }

        return new ImportTemplate(
            "MiddiFranchi-import-template.zip",
            memory.ToArray());
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string content,
        bool addBom)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();

        if (addBom)
        {
            stream.Write(Utf8Bom);
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);
    }

    private const string Readme =
        """
        MiddiFranchi - modello di importazione
        ======================================

        Contenuto dell'archivio
        -----------------------
        accounts.csv             conti
        account_balances.csv     saldi nel tempo dei conti
        instruments.csv          strumenti finanziari
        instrument_prices.csv    quotazioni nel tempo degli strumenti
        operations.csv           acquisti e vendite

        Regole generali
        ---------------
        - Formato CSV, codifica UTF-8, separatore virgola, prima riga di intestazione.
        - Date nel formato yyyy-MM-dd.
        - Numeri con il punto come separatore decimale (es. 1234.56).
        - Valuta sempre EUR.
        - I conti sono riconosciuti dal NOME (senza maiuscole/minuscole): importare di nuovo
          un conto con lo stesso nome aggiorna le sue impostazioni.
        - In account_balances.csv il saldo è identificato dal nome del conto e dalla data.
        - Gli strumenti sono riconosciuti dall'ISIN, obbligatorio: reimportando lo stesso
          ISIN la riga viene aggiornata.
        - In quotazioni e operazioni lo strumento si referenzia con instrument_isin; se
          manca si usa instrument_symbol (ticker) e infine instrument_name. Se nessuno dei
          tre corrisponde a uno strumento del file o dell'applicazione la riga è in errore.
        - Le quotazioni sono identificate dallo strumento e dalla data: reimportando la
          stessa coppia la quotazione viene aggiornata.
        - Le operazioni sono identificate da strumento, tipo, data, quantità, prezzo e
          commissioni: le righe già presenti vengono saltate.
        - I campi opzionali possono essere vuoti.
        - I file mancanti vengono trattati come sezioni vuote.

        accounts.csv
        ------------
        name,type,status,color_hex,icon,loans
        type: Checking, Card, Cash, Liability, Loans
        status: Active, Closed (default Active)
        color_hex: #RRGGBB oppure vuoto
        icon: wallet, icon:wallet oppure un data URL; vuoto = icona predefinita
        loans: solo per conti Loans; nomi dei prestiti separati da ; senza valori;

        account_balances.csv
        --------------------
        account_name,date,amount
        Il conto si referenzia con il suo NOME. La data non può essere nel futuro.
        Il saldo è identificato da nome conto e data: reimportando lo stesso file la
        riga viene aggiornata. I saldi dei conti Loans possono essere ricostruiti dai
        prestiti all'avvio.

        instruments.csv
        ---------------
        name,asset_class,price_unit,isin
        Colonne facoltative: status, symbol, quote_currency, notional_value,
        notional_currency, reference_url, notes, maturity_date, coupon_rate, tax_rate.
        Chiave: isin, obbligatorio.
        asset_class: Equity, Bond, MoneyMarket, RealEstate, Commodity, Crypto, Mixed,
                     Pension, Other
        status: Active, Archived (default Active)
        quote_currency: EUR (facoltativa, default EUR)
        price_unit: PerUnit, PercentOfNominal
        notional_value/notional_currency: obbligatori per PercentOfNominal
        maturity_date/coupon_rate: solo per Bond
        tax_rate: aliquota di tassazione in percentuale (0-100, default 26)

        instrument_prices.csv
        ---------------------
        date,price[,instrument_isin][,instrument_symbol][,instrument_name][,currency][,source]
        Lo strumento si cerca per ISIN, poi per simbolo e infine per nome.
        Obbligatorio almeno uno tra instrument_isin, instrument_symbol e instrument_name.
        currency: EUR per PerUnit, vuota per PercentOfNominal
        source: etichetta libera (default Import)

        operations.csv
        --------------
        kind,date,quantity,price[,instrument_isin][,instrument_symbol][,instrument_name]
        [,fee][,description]
        Lo strumento si cerca per ISIN, poi per simbolo e infine per nome.
        Obbligatorio almeno uno tra instrument_isin, instrument_symbol e instrument_name.
        Colonne facoltative: fee, description.
        kind: Buy, Sell
        Le operazioni vengono importate in ordine di data. La catena acquisti/vendite
        deve essere coerente; eventuali errori annullano l'intera importazione.

        Comportamento
        -------------
        L'importazione non elimina dati esistenti: i conti e i saldi vengono inseriti o
        aggiornati per nome (i saldi anche per data), gli strumenti per ISIN.
        Quotazioni e operazioni vengono collegate allo strumento per ISIN, simbolo o nome.
        Le operazioni già presenti vengono saltate.
        L'intera importazione è transazionale: in caso di errore non resta nulla.
        """;
}
