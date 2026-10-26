# Importazione conti

La pagina **Configurazione → Dati e backup → Importa Dati → Importa conti (per singole sezioni)**
(`/settings/data/import/accounts`) importa tutti i conti e le loro impostazioni da un
singolo file CSV.

Dalla pagina si può scaricare un **modello** già pronto da compilare
(`MiddiFranchi-import-conti.csv`).

## Formato

CSV UTF-8 (con o senza BOM), separatore virgola, prima riga di intestazione:

```
name,type,status,color_hex,icon,loans
```

| Campo | Obbligatorio | Note |
|---|---|---|
| name | sì | nome del conto, univoco |
| type | sì | `Checking`, `Card`, `Cash`, `Liability`, `Loans` |
| status | no | `Active` (default), `Closed` |
| color_hex | no | `#RRGGBB` oppure vuoto (colore automatico) |
| icon | no | `wallet`, `icon:wallet` oppure un data URL `data:image/...`; vuoto = icona predefinita |
| loans | no | solo per conti di tipo `Loans`: nomi dei prestiti (debitori) separati da `;`, **senza valori**; vuoto = nessun prestito aggiunto |

La valuta è sempre `EUR`.

Attenzione: se un campo contiene una virgola (es. un data URL base64) va racchiuso tra
virgolette.

Esempio:

```
name,type,status,color_hex,icon,loans
Directa,Checking,Active,,
PostePay,Card,Closed,#e67e22,icon:credit-card
Euro,Cash,Active,#34a853,wallet
Prestiti personali,Loans,Active,,,Francesca;Martina
```

## Comportamento

- Chiave = nome (senza distinzione di maiuscole/minuscole): un conto già presente con lo stesso
  nome viene **aggiornato** (tipo, stato, colore e icona); uno nuovo viene **creato**. Il nome
  resta quello del database, quindi un conto non può essere rinominato da questa importazione.
- **Prestiti**: i prestiti vengono creati con il solo nome, senza importi né rate: saldo e rate
  si registrano poi nell'applicazione. Se il nome è già presente sul conto non viene duplicato;
  i prestiti esistenti non elencati nel file restano invariati né vengono eliminati.
- **Non distruttiva**: i conti che non sono nel file non vengono toccati né eliminati.
- **Atomica**: l'intera importazione avviene in una transazione; un errore non lascia dati
  parziali.
- **Validazione preventiva**: la pagina analizza il file (intestazioni, tipo, stato, colore,
  icona, nomi duplicati) e mostra gli errori prima dell'importazione.
- Un conto di tipo `Loans` con prestiti registrati non può essere convertito a un altro tipo.
- I suoi saldi vengono comunque ricostruiti dai prestiti all'avvio.
- Il servizio di aggiornamento prezzi automatico è sospeso durante l'importazione.

## Differenze con l'import "vecchia applicazione"

L'import completo dell'archivio ([data-import.md](data-import.md)) e questa importazione
dedicata usano entrambi il **nome del conto** come chiave: il file dei conti ha lo stesso
formato e può essere usato in entrambe le pagine.
