# Importazione dati da una vecchia applicazione

La pagina **Configurazione → Dati e backup → Importa Dati**
(`/settings/data/import`) importa conti, saldi storici, strumenti, quotazioni e operazioni
(acquisti e vendite) da un archivio ZIP o dai singoli file CSV.

Dalla pagina si può scaricare un **modello** già compilato con un esempio completo.

## Archivio

Estensione consigliata: `.mfimport` (è un normale ZIP). Contiene:

```
accounts.csv
account_balances.csv
instruments.csv
instrument_prices.csv
operations.csv
```

I file possono essere in una sottocartella; il nome del file viene riconosciuto. I file mancanti
sono trattati come sezioni vuote.

In alternativa allo ZIP puoi caricare **direttamente i file CSV**, anche tutti insieme dalla stessa
finestra di selezione. I nomi devono essere quelli indicati sopra.

## Regole generali

- CSV UTF-8, separatore virgola, prima riga di intestazione.
- Date nel formato `yyyy-MM-dd`.
- Numeri con il punto decimale e senza simbolo di valuta (es. `1234.56`).
- Valuta sempre `EUR`.
- I **conti** sono riconosciuti dal **nome del conto** (senza distinzione di maiuscole/minuscole):
  lo stesso nome importato di nuovo aggiorna le impostazioni del conto. Nei campi che
  referenziano un conto (saldi) il valore è il nome del conto.
- In `account_balances.csv` il saldo è identificato dal **nome del conto** e dalla **data**.
- In `instruments.csv` la chiave è l'**ISIN**, obbligatorio: reimportando lo stesso ISIN lo
  strumento viene aggiornato.
- In `instrument_prices.csv` e `operations.csv` lo strumento si cerca nell'ordine per
  **`instrument_isin`**, **`instrument_symbol`** (ticker) e **`instrument_name`**. Serve almeno
  uno dei tre campi; se nessuno corrisponde a uno strumento del file o dell'applicazione la
  riga è in errore. Se un nome corrisponde a più strumenti la riga è in errore: indica l'ISIN.
- Le operazioni sono riconosciute da strumento, tipo, data, quantità, prezzo e commissioni.
- I campi opzionali possono essere vuoti.
- Non sono ammessi `float`/`double`: gli importi sono decimali.

## accounts.csv

```
name,type,status,color_hex,icon,loans
```

| Campo | Obbligatorio | Note |
|---|---|---|
| name | sì | nome del conto, univoco; è la chiave che aggiorna i conti già presenti |
| type | sì | `Checking`, `Card`, `Cash`, `Liability`, `Loans` |
| status | no | `Active` (default), `Closed` |
| color_hex | no | `#RRGGBB` |
| icon | no | `wallet`, `icon:wallet` o data URL; vuoto = icona predefinita |
| loans | no | solo per conti `Loans`: nomi dei prestiti (debitori) separati da `;`, senza valori |

Esempio:

```
name,type,status,color_hex,icon,loans
Directa,Checking,Active,,
Prestiti personali,Loans,Active,,,Francesca;Martina
```

## account_balances.csv

```
account_name,date,amount
```

Una riga per ogni saldo storico. `account_name` è il **nome del conto**, che deve essere
presente nello stesso archivio o già registrato. La data non può essere nel futuro. L'importo
può essere negativo (es. passività). Il saldo è identificato dal nome del conto e dalla data:
reimportare la stessa coppia aggiorna il saldo esistente.

```
account_name,date,amount
Directa,2024-01-01,1000.00
Directa,2024-02-01,1250.50
```

I saldi dei conti di tipo `Loans` possono essere ricostruiti dai prestiti all'avvio
dell'applicazione: non sono quindi la fonte di verità per quel tipo di conto.

## instruments.csv

```
name,asset_class,price_unit,isin[,altre colonne facoltative]
```

Obbligatorie: `name`, `asset_class`, `price_unit` e `isin`. La chiave è l'**ISIN**.
Colonne facoltative: `status`, `symbol`,
`quote_currency`, `notional_value`, `notional_currency`, `reference_url`, `notes`,
`maturity_date`, `coupon_rate`, `tax_rate`.

| Campo | Note |
|---|---|
| asset_class | `Equity`, `Bond`, `MoneyMarket`, `RealEstate`, `Commodity`, `Crypto`, `Mixed`, `Pension`, `Other` |
| status | `Active` (default), `Archived` |
| symbol | massimo 32 caratteri, univoco |
| isin | 12 caratteri alfanumerici, univoco; chiave dello strumento |
| quote_currency | `EUR` (facoltativa, default `EUR`) |
| price_unit | `PerUnit`, `PercentOfNominal` |
| notional_value / notional_currency | obbligatori per `PercentOfNominal`; valuta `EUR` |
| maturity_date / coupon_rate | ammessi solo per `Bond` |

Esempio:

```
name,asset_class,price_unit,isin,status,symbol
ETF esempio,Equity,PerUnit,IT0000000001,Active,EXMP
```

## instrument_prices.csv

```
date,price[,instrument_isin][,instrument_symbol][,instrument_name][,currency][,source]
```

Lo strumento si cerca per **ISIN**, poi per **simbolo** e infine per **nome**; serve almeno
uno dei tre campi `instrument_*`. Se nessuno corrisponde la riga è in errore. La quotazione è
identificata dallo **strumento e dalla data**: reimportando la stessa coppia viene aggiornata
invece di essere duplicata.

- `price` positivo.
- `currency`: `EUR` per `PerUnit`, vuota per `PercentOfNominal`.
- `source`: etichetta libera (default `Import`).

Esempio:

```
instrument_isin,instrument_symbol,instrument_name,date,price,currency,source
IT0000000001,EXMP,ETF esempio,2024-01-02,100.50,EUR,Import
```

## operations.csv

```
kind,date,quantity,price[,instrument_isin][,instrument_symbol][,instrument_name][,fee][,description]
```

Lo strumento si cerca per **ISIN**, poi per **simbolo** e infine per **nome**; serve almeno
uno dei tre campi `instrument_*`. Se nessuno corrisponde la riga è in errore.

- `kind`: `Buy` o `Sell`.
- `quantity` e `price` positivi.
- `fee` è la commissione totale (una sola componente); vuoto = 0.
- `description` opzionale.
- Le operazioni sono riconosciute da **strumento, tipo, data, quantità, prezzo e commissioni**:
  una riga identica a un'operazione già registrata viene saltata.

Esempio:

```
kind,date,instrument_isin,instrument_symbol,instrument_name,quantity,price,fee,description
Buy,2024-01-03,IT0000000001,EXMP,ETF esempio,10,100.50,2.50,Acquisto iniziale
```

Le operazioni vengono importate **in ordine di data** (a parità di data, prima gli acquisti).
La catena acquisti/vendite deve essere coerente: una vendita non può superare le quote possedute.
Eventuali errori annullano l'intera importazione.

## Comportamento

- **Idempotente**: i conti vengono inseriti o aggiornati per **nome** (i prestiti elencati
  nell'apposita colonna vengono creati con il solo nome, senza valori, se non esistono già);
  i saldi per nome conto e data; gli strumenti per **ISIN**; le quotazioni per strumento e
  data; le operazioni per strumento, tipo, data, quantità, prezzo e commissioni. Le operazioni
  già presenti vengono saltate.
- **Non distruttiva**: non elimina dati esistenti.
- **Atomica**: l'intera importazione avviene in una transazione; un errore non lascia dati parziali.
- **Validazione preventiva**: la pagina analizza l'archivio, controlla intestazioni, tipi, date,
  riferimenti incrociati, ISIN e simboli duplicati e mostra gli errori prima dell'importazione.
- Il servizio di aggiornamento prezzi è sospeso durante l'importazione.

## Limiti

- Solo EUR.
- Solo acquisti e vendite (non dividendi, cedole, trasferimenti o commissioni autonome).
- Una sola commissione per operazione.
- I nomi dei conti e gli ISIN degli strumenti sono le chiavi stabili: importare un conto con
  un nome diverso crea un conto nuovo, importare uno strumento con un ISIN diverso crea un
  altro strumento. Per quotazioni e operazioni il collegamento avviene per ISIN, simbolo o
  nome; se il nome o il simbolo non bastano a distinguere lo strumento, indica l'ISIN.
