# Importazione dati da una vecchia applicazione

La pagina **Configurazione → Dati e backup → Importa da vecchia applicazione**
(`/settings/data/import`) importa conti, saldi storici, strumenti, quotazioni e operazioni
(acquisti e vendite) da un archivio ZIP di file CSV.

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
- `external_id` obbligatorio, univoco all'interno del file e **stabile** nel tempo.
  È la chiave che rende l'importazione idempotente: lo stesso `external_id` aggiorna la stessa
  riga. ID esterni diversi creano righe diverse.
- I campi opzionali possono essere vuoti.
- Non sono ammessi `float`/`double`: gli importi sono decimali.

## accounts.csv

```
external_id,name,type,status,color_hex,icon
```

| Campo | Obbligatorio | Note |
|---|---|---|
| external_id | sì | stabile e univoco |
| name | sì | nome del conto |
| type | sì | `Checking`, `Card`, `Cash`, `Liability`, `Loans` |
| status | no | `Active` (default), `Closed` |
| color_hex | no | `#RRGGBB` |
| icon | no | `wallet`, `icon:wallet` o data URL; vuoto = icona predefinita |

Esempio:

```
external_id,name,type,status,color_hex,icon
conto-1,Conto corrente,Checking,Active,#2563eb,wallet
```

## account_balances.csv

```
external_id,account_external_id,date,amount
```

Una riga per ogni saldo storico. `account_external_id` deve riferirsi a un conto presente nello
stesso archivio o già importato. La data non può essere nel futuro. L'importo può essere negativo
(es. passività).

Esempio:

```
external_id,account_external_id,date,amount
saldo-1,conto-1,2024-01-01,1000.00
saldo-2,conto-1,2024-02-01,1250.50
```

I saldi dei conti di tipo `Loans` possono essere ricostruiti dai prestiti all'avvio
dell'applicazione: non sono quindi la fonte di verità per quel tipo di conto.

## instruments.csv

```
external_id,name,asset_class,status,symbol,isin,quote_currency,price_unit,
notional_value,notional_currency,reference_url,notes,maturity_date,coupon_rate
```

| Campo | Note |
|---|---|
| asset_class | `Equity`, `Bond`, `MoneyMarket`, `RealEstate`, `Commodity`, `Crypto`, `Mixed`, `Pension`, `Other` |
| status | `Active` (default), `Archived` |
| symbol | massimo 32 caratteri, univoco |
| isin | 12 caratteri alfanumerici, univoco |
| quote_currency | `EUR` |
| price_unit | `PerUnit`, `PercentOfNominal` |
| notional_value / notional_currency | obbligatori per `PercentOfNominal`; valuta `EUR` |
| maturity_date / coupon_rate | ammessi solo per `Bond` |

Esempio:

```
external_id,name,asset_class,status,symbol,isin,quote_currency,price_unit,notional_value,notional_currency,reference_url,notes,maturity_date,coupon_rate
etf-1,ETF esempio,Equity,Active,EXMP,IT0000000001,EUR,PerUnit,,,,,,
```

## instrument_prices.csv

```
external_id,instrument_external_id,date,price,currency,source
```

- `price` positivo.
- `currency`: `EUR` per `PerUnit`, vuota per `PercentOfNominal`.
- `source`: etichetta libera (default `Import`).

Esempio:

```
external_id,instrument_external_id,date,price,currency,source
prezzo-1,etf-1,2024-01-02,100.50,EUR,Import
```

## operations.csv

```
external_id,kind,date,instrument_external_id,settlement_account_external_id,
quantity,price,fee,description
```

- `kind`: `Buy` o `Sell`.
- `quantity` e `price` positivi.
- `fee` è la commissione totale (una sola componente); vuoto = 0.
- `description` opzionale.

Esempio:

```
external_id,kind,date,instrument_external_id,settlement_account_external_id,quantity,price,fee,description
operazione-1,Buy,2024-01-03,etf-1,conto-1,10,100.50,2.50,Acquisto iniziale
```

Le operazioni vengono importate **in ordine di data** (a parità di data, prima gli acquisti).
La catena acquisti/vendite deve essere coerente: una vendita non può superare le quote possedute.
Eventuali errori annullano l'intera importazione.

## Comportamento

- **Idempotente**: conti, saldi, strumenti e quotazioni vengono inseriti o aggiornati per
  `external_id`; le operazioni già presenti vengono saltate.
- **Non distruttiva**: non elimina dati esistenti.
- **Atomica**: l'intera importazione avviene in una transazione; un errore non lascia dati parziali.
- **Validazione preventiva**: la pagina analizza l'archivio, controlla intestazioni, tipi, date,
  riferimenti incrociati, ISIN e simboli duplicati e mostra gli errori prima dell'importazione.
- Il servizio di aggiornamento prezzi è sospeso durante l'importazione.

## Limiti

- Solo EUR.
- Solo acquisti e vendite (non dividendi, cedole, trasferimenti o commissioni autonome).
- Una sola commissione per operazione.
- Gli identificativi esterni non devono cambiare tra un'importazione e l'altra.
