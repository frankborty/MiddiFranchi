# Dati e backup

La sezione **Configurazione → Dati e backup** (`/settings/data`) gestisce l'esportazione, il
ripristino e l'eliminazione dei dati.

## Sezioni

Un backup può contenere una o più sezioni:

| Sezione | Contenuto |
|---|---|
| Conti | Anagrafica conti (`Accounts`) |
| Categorie | Categorie e sottocategorie (`Categories`) |
| Strumenti finanziari | Anagrafica strumenti (`Instruments`) |
| Movimenti | Operazioni, posting e commissioni (`FinancialOperations`, `AccountPostings`, `InvestmentOperationFees`) |
| Storico valore conti | Saldi manuali, prestiti e letture (`ManualBalances`, `Loans`, `LoanReadings`) |
| Storico valore strumenti | Quotazioni (`PriceObservations`) |
| Impostazioni prezzi | Orario, frequenza e ultimo stato del servizio (`price-update.json`) |

Il file contiene solo le sezioni selezionate.

## Formato del file

Un backup è un **singolo file** `MiddiFranchi-backup-yyyyMMdd-HHmmss.mfbackup`, cioè un archivio ZIP
con sezioni JSON:

```
manifest.json
data/accounts.json
data/categories.json
data/instruments.json
data/financial-operations.json
data/account-postings.json
data/fee-components.json
data/price-observations.json
data/manual-balances.json
data/loans.json
data/loan-readings.json
settings/price-updates.json
```

Il manifest riporta formato (`formatVersion`), versione applicazione, schema EF, data di creazione,
nota opzionale, elenco delle sezioni incluse, conteggi e checksum SHA-256 per ogni sezione.

Il file **non** è cifrato e **non** contiene connection string, credenziali o `__EFMigrationsHistory`.
I backup creati con il vecchio formato (versione 1) non sono compatibili: vanno rigenerati.

## Ripristino completo e parziale

Il file contiene una o più sezioni. Il comportamento dipende da quali sezioni sono presenti:

### Ripristino completo

Se il file contiene **tutte** le sezioni dati (Conti, Categorie, Strumenti, Movimenti, Storico valore
conti, Storico valore strumenti), il ripristino è **completo**:

- tutti i dati attuali delle 10 tabelle applicative vengono eliminati;
- i dati del backup vengono reinseriti in un'unica transazione;
- i dati presenti nel database che non sono nel backup **vanno persi**;
- le impostazioni del servizio prezzi vengono importate solo se la sezione è inclusa.

In pratica il database assume lo stato del backup. Se qualcosa fallisce (checksum, schema,
riferimenti mancanti, errore DB) la transazione viene annullata e i dati precedenti restano intatti.

### Ripristino parziale

Se il file contiene solo alcune sezioni:

- **Conti, Categorie, Strumenti**: le righe del backup vengono **inserite o aggiornate per Id**
  (upsert). I dati esistenti non presenti nel backup **restano**.
- **Movimenti, Storico valore conti, Storico valore strumenti**: le tabelle della sezione vengono
  **svuotate e sostituite** con i dati del backup. I dati esistenti di quella sezione non presenti
  nel backup vanno persi.
- Le sezioni non incluse nel file non vengono toccate.

Prima di applicare il file l'app verifica che i riferimenti (conti, categorie, strumenti) esistano
nel database o siano inclusi nel backup. Se mancano, il ripristino viene bloccato con un messaggio
esplicativo.

## Procedura

1. Aprire `/settings/data`.
2. Per il backup: selezionare le sezioni, eventualmente una nota, e premere **Scarica backup**.
3. Per il ripristino: caricare il file, premere **Analizza backup**, controllare sezioni e modalità,
   quindi **Ripristina…** e digitare `RIPRISTINA`.

Il ripristino avviene in una singola transazione PostgreSQL. Qualsiasi errore prima del commit
annulla l'intera operazione. Le impostazioni del servizio prezzi vengono importate dopo il commit;
un errore in questo passaggio produce un avviso ma non invalida i dati ripristinati.

## Nuovo nodo

1. Distribuire la **stessa versione** dell'applicazione.
2. Configurare PostgreSQL e `ConnectionStrings__DefaultConnection` (user secrets o variabile
   d'ambiente).
3. Applicare le migration:
   ```
   dotnet ef database update --project src/MiddiFranchi.Infrastructure \
     --startup-project src/MiddiFranchi.Web
   ```
4. Avviare l'applicazione e aprire `/settings/data`.
5. Caricare il file `.mfbackup` e completare il ripristino.

Il ruolo DB deve essere owner delle tabelle: il wipe usa `TRUNCATE`. Il ruolo che applica le
migration normalmente lo è.

## Eliminazione selettiva

L'eliminazione è indipendente dal backup:

| Dataset | Tabelle | Dipendenze |
|---|---|---|
| Movimenti | `FinancialOperations`, `AccountPostings`, `InvestmentOperationFees` | nessuna |
| Strumenti finanziari | `Instruments`, `PriceObservations` | richiede Movimenti |
| Conti | `Accounts`, `ManualBalances`, `Loans`, `LoanReadings` | richiede Movimenti |
| Categorie | `Categories` | richiede Movimenti |
| Tutto | tutte le tabelle precedenti + `AppMetadata` | tutte |

Le sezioni vengono sostituite con `TRUNCATE ... RESTART IDENTITY` in una transazione. Se si
selezionano Conti, Strumenti o Categorie, l'app include automaticamente Movimenti e mostra un avviso.
L'operazione richiede la digitazione di `ELIMINA`.

Dopo un'eliminazione i valori predefiniti non vengono ricreati automaticamente: i seeder usano
marker versionati in `AppMetadata`. L'azione **Ripristina valori predefiniti** rimuove i marker e
riesegue i seeder per ricreare i conti Cash/Prestiti e le categorie di base.

## Note operative

- Non usare altre schede dell'app durante un ripristino.
- Il servizio di aggiornamento prezzi è sospeso durante backup, ripristino ed eliminazione tramite un
  gate condiviso.
- Il formato è versionato: un backup creato con uno schema diverso viene rifiutato.
- Il limite di caricamento è 256 MB.
- Conserva il file in un luogo sicuro: non è cifrato.
