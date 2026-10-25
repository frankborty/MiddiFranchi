# Prova locale con Docker Desktop

Questa configurazione avvia **MiddiFranchi** e un **PostgreSQL 17** in due container Docker Desktop,
senza registry e senza toccare il database di sviluppo.

## Avvio

Dalla root del repository:

```bash
docker compose -f compose.local.yaml up -d --build
```

La prima volta Docker costruisce l'immagine dell'app dal `Dockerfile` e scarica `postgres:17` se non
è già presente. Attendi che il database sia healthy: l'app parte dopo e applica le migrazioni.

### Nota PowerShell

In Windows PowerShell un messaggio come `Image middifranchi-local-app Building` con
`NativeCommandError` **non è un errore di build**: Docker Compose scrive il progresso su stderr e
PowerShell lo trasforma in errore quando si usa `2>&1`. Lancia il comando senza `2>&1`:

```powershell
docker compose -f compose.local.yaml up -d --build app
```

oppure, se hai bisogno di catturare anche stderr:

```powershell
cmd /c "docker compose -f compose.local.yaml up -d --build app 2>&1"
```

```bash
docker compose -f compose.local.yaml ps
docker compose -f compose.local.yaml logs -f app
```

Quando nei log compare `Now listening on: http://[::]:8080`, apri:

**http://localhost:8080**

Il database nuovo viene creato dalle migrazioni senza movimenti, strumenti o quotazioni
dimostrative. Restano i conti speciali **Cash** e **Prestiti** e le categorie di base, necessari
al funzionamento dell'app.

## Primo avvio

La home, **Movimenti** e **Investimenti** partono vuoti.

- I conti speciali Cash e Prestiti e le categorie di base sono dati di configurazione, non
  movimenti.
- Per iniziare con i tuoi dati usa **Configurazione → Dati e backup → Importa** oppure ripristina
  un backup completo.
- Per aggiungere un conto: **Conti → Gestisci conti → Nuovo conto**.
- Per aggiungere un investimento: **Investimenti → Nuovo strumento**, poi apri lo strumento e
  premi **Registra operazione**.
- `down -v` ricrea un database pulito: le migrazioni non inseriscono più movimenti dimostrativi.

## Credenziali del database locale

| Voce | Valore predefinito |
|---|---|
| Host (dentro Docker) | `db` |
| Porta | `5432` |
| Database | `middifranchi` |
| Utente | `middifranchi` |
| Password | `middifranchi-local` |

La porta 5432 **non** è pubblicata sull'host: l'app la raggiunge dalla rete interna di Compose.
Per ispezionare il DB:

```bash
docker compose -f compose.local.yaml exec db psql -U middifranchi -d middifranchi
```

Per cambiare password o porta dell'app, imposta le variabili prima di `up`:

```powershell
$env:POSTGRES_PASSWORD = "una-password-locale"
$env:APP_PORT = "8081"
docker compose -f compose.local.yaml up -d --build
```

In alternativa puoi creare un file `.env` (già ignorato da git) con:

```text
POSTGRES_PASSWORD=una-password-locale
APP_PORT=8081
```

Se cambi la password dopo che il volume PostgreSQL è già stato inizializzato, il container non la
aggiorna: per un test pulito usa `down -v` e riparti, oppure cambiala con `ALTER ROLE`.

## Dati reali nel container

Il database del container è separato da quello di sviluppo. Per provare i tuoi dati:

1. Nell'ambiente esistente apri **Configurazione → Dati e backup** e scarica un backup completo.
2. Nell'app su `http://localhost:8080` apri la stessa pagina e ripristina il backup.

In alternativa puoi usare `pg_dump`/`pg_restore` dal container PostgreSQL.

## Stop e reset

Ferma i container mantenendo i dati:

```bash
docker compose -f compose.local.yaml down
```

Rimuovi anche database e volumi (operazione distruttiva, solo per un test pulito):

```bash
docker compose -f compose.local.yaml down -v
```

## Note

- L'app usa `ASPNETCORE_ENVIRONMENT=Production` e ascolta su HTTP 8080; non c'è HTTPS locale.
- Le chiavi Data Protection e `price-update.json` sono nei volumi
  `middifranchi-local-dataprotection` e `middifranchi-local-appdata`.
- Se la porta 8080 è occupata, usa `APP_PORT`.
- Questa configurazione è solo per Docker Desktop locale; per Portainer e un PostgreSQL esterno usa
  [deployment.md](deployment.md).
