using System.IO.Compression;
using System.Text;

namespace MiddiFranchi.Application.DataImport;

internal static class ImportTemplateBuilder
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static ImportTemplate Build()
    {
        var accounts = CsvDocument.Write(
            ["external_id", "name", "type", "status", "color_hex", "icon"],
            [
                new string?[] { "conto-esempio", "Conto esempio", "Checking", "Active", "#2563eb", "wallet" },
            ]);

        var balances = CsvDocument.Write(
            ["external_id", "account_external_id", "date", "amount"],
            [
                new string?[] { "saldo-esempio", "conto-esempio", "2024-01-01", "1000.00" },
            ]);

        var instruments = CsvDocument.Write(
            [
                "external_id", "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate", "tax_rate",
            ],
            [
                new string?[]
                {
                    "etf-esempio", "ETF esempio", "Equity", "Active", "EXMP", "IT0000000001",
                    "EUR", "PerUnit", "", "", "", "", "", "", "26",
                },
            ]);

        var prices = CsvDocument.Write(
            ["external_id", "instrument_external_id", "date", "price", "currency", "source"],
            [
                new string?[] { "prezzo-esempio", "etf-esempio", "2024-01-02", "100.50", "EUR", "Import" },
            ]);

        var operations = CsvDocument.Write(
            [
                "external_id", "kind", "date", "instrument_external_id",
                "settlement_account_external_id", "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "operazione-esempio", "Buy", "2024-01-03", "etf-esempio", "conto-esempio",
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
        - external_id è obbligatorio, univoco all'interno del file e stabile nel tempo.
          Lo stesso external_id importato di nuovo aggiorna la stessa riga.
        - I campi opzionali possono essere vuoti.
        - I file mancanti vengono trattati come sezioni vuote.

        accounts.csv
        ------------
        external_id,name,type,status,color_hex,icon
        type: Checking, Card, Cash, Liability, Loans
        status: Active, Closed (default Active)
        color_hex: #RRGGBB oppure vuoto
        icon: wallet, icon:wallet oppure un data URL; vuoto = icona predefinita

        account_balances.csv
        --------------------
        external_id,account_external_id,date,amount
        La data non può essere nel futuro. I saldi dei conti Loans possono essere
        ricostruiti dai prestiti all'avvio.

        instruments.csv
        ---------------
        external_id,name,asset_class,status,symbol,isin,quote_currency,price_unit,
        notional_value,notional_currency,reference_url,notes,maturity_date,coupon_rate,tax_rate
        asset_class: Equity, Bond, MoneyMarket, RealEstate, Commodity, Crypto, Mixed,
                     Pension, Other
        status: Active, Archived (default Active)
        quote_currency: EUR
        price_unit: PerUnit, PercentOfNominal
        notional_value/notional_currency: obbligatori per PercentOfNominal
        maturity_date/coupon_rate: solo per Bond
        tax_rate: aliquota di tassazione in percentuale (0-100, default 26)

        instrument_prices.csv
        ---------------------
        external_id,instrument_external_id,date,price,currency,source
        currency: EUR per PerUnit, vuota per PercentOfNominal
        source: etichetta libera (default Import)

        operations.csv
        --------------
        external_id,kind,date,instrument_external_id,settlement_account_external_id,
        quantity,price,fee,description
        kind: Buy, Sell
        Le operazioni vengono importate in ordine di data. La catena acquisti/vendite
        deve essere coerente; eventuali errori annullano l'intera importazione.

        Comportamento
        -------------
        L'importazione non elimina dati esistenti: inserisce o aggiorna per external_id.
        Le operazioni già presenti (stesso external_id) vengono saltate.
        L'intera importazione è transazionale: in caso di errore non resta nulla.
        """;
}
