# MiddiFranchi

MiddiFranchi è un progetto **educativo** di finanza personale: un'applicazione web per
registrare conti, movimenti, investimenti e il patrimonio nel tempo. Non è un prodotto
commerciale e non è pensato per essere usato con dati reali senza una verifica autonoma.

## Come è stato realizzato

L'intero progetto è stato sviluppato **tramite agent code**. Non è stata aperta una UI per
verificare il risultato e il codice non è stato controllato manualmente riga per riga.
Il repository esiste per mostrare un possibile flusso di lavoro con agenti di coding su
un'applicazione .NET completa, non come esempio di codice certificato o pronto per la
produzione.

Di conseguenza possono esserci imperfezioni, scelte non ottimali e comportamenti non
verificati in ogni scenario.

## Stato del progetto e supporto

Questo progetto è fornito **così com'è**, senza garanzie e senza supporto.

**Bug, problemi, richieste di assistenza o segnalazioni non verranno presi in carico.**
Non aprire issue per chiedere correzioni: il progetto è condiviso esclusivamente a scopo
educativo. Se vuoi usarlo, studialo, modificalo e adattalo in autonomia.

## A cosa serve

Il progetto mostra come modellare e implementare, in modo incrementale:

- conti, saldi manuali e movimenti finanziari;
- entrate, uscite, trasferimenti, storni e categorie;
- strumenti finanziari, quotazioni, acquisti, vendite, cedole e dividendi;
- prestiti e rilevazioni del patrimonio;
- importazione e backup/ripristino dei dati;
- una UI Blazor con dashboard, filtri persistenti e preferenze di aspetto;
- separazione tra dominio, applicazione, infrastruttura e presentazione;
- persistenza PostgreSQL con Entity Framework Core e migrazioni;
- test unitari e test di integrazione con PostgreSQL tramite Testcontainers.

## Stack

- **C# / .NET 10**
- **Blazor Web App** con rendering Interactive Server
- **Clean Architecture** (Domain, Application, Infrastructure, Web)
- **Entity Framework Core** con **Npgsql**
- **PostgreSQL 17**
- **xUnit** e **Shouldly**
- **Docker / Docker Compose**

## Struttura

```text
MiddiFranchi/
├── src/
│   ├── MiddiFranchi.Domain/           Entità, value object e regole di dominio
│   ├── MiddiFranchi.Application/      Casi d'uso, servizi applicativi e interfacce
│   ├── MiddiFranchi.Infrastructure/   EF Core, Npgsql, persistenza e integrazioni
│   └── MiddiFranchi.Web/              Blazor Web App (Interactive Server)
├── tests/
│   ├── MiddiFranchi.UnitTests/        Test di dominio e applicazione
│   └── MiddiFranchi.IntegrationTests/ Test di integrazione con PostgreSQL
├── docs/                              Documentazione e decisioni architetturali
├── import/                            File di import locali (ignorati da Git)
├── compose.local.yaml                 Stack Docker locale (app + PostgreSQL)
├── compose.yaml                       Stack Docker per il deployment
├── MiddiFranchi.sln
├── AGENTS.md
└── README.md
```

### Regole di dipendenza

- `Domain` non dipende da altri progetti della soluzione.
- `Application` dipende solo da `Domain`.
- `Infrastructure` dipende da `Application` e `Domain`.
- `Web` dipende da `Application` e `Infrastructure`.
- I test unitari dipendono da `Domain` e `Application`.
- I test di integrazione dipendono da `Domain`, `Application` e `Infrastructure`.

## Privacy e dati personali

Il repository **non contiene dati personali o finanziari reali**. I file di import locali
sono esclusi tramite `.gitignore` (`import/`). Le migrazioni non contengono importi, ISIN o
strumenti riconducibili a un portafoglio reale.

Per usare l'applicazione:

- usa un database tuo e non condividere connection string o credenziali;
- conserva i segreti con .NET user secrets o variabili d'ambiente;
- non committare backup, estratti conto o file di import;
- non usare dati reali senza aver verificato autonomamente il comportamento dell'app.

## Prerequisiti

- .NET 10 SDK
- PostgreSQL per eseguire l'applicazione
- Docker Desktop per lo stack locale e per i test di integrazione PostgreSQL
- Facoltativo: Visual Studio, JetBrains Rider o VS Code

## Build e test

Dalla root del repository:

```bash
dotnet restore MiddiFranchi.sln
dotnet build MiddiFranchi.sln --configuration Release --no-restore
dotnet test MiddiFranchi.sln --configuration Release --no-build
```

I test di integrazione PostgreSQL sono opt-in e richiedono Docker:

```powershell
$env:MIGDIFRANCHI_POSTGRES_TESTS = "1"
dotnet test tests/MiddiFranchi.IntegrationTests/MiddiFranchi.IntegrationTests.csproj --configuration Release
```

I test usano Testcontainers: creano un container PostgreSQL isolato, applicano le migrazioni
e isolano i dati di ogni test.

## Avvio locale con Docker

```bash
docker compose -f compose.local.yaml up -d --build
```

L'applicazione è disponibile su `http://localhost:8080`. La configurazione locale usa un
database PostgreSQL nel container e applica le migrazioni all'avvio.

Per fermare lo stack:

```bash
docker compose -f compose.local.yaml down
```

Per un deployment generico, vedere [compose.yaml](compose.yaml) e
[docs/deployment.md](docs/deployment.md).

## Configurazione

La connection string viene letta da `ConnectionStrings:DefaultConnection`.

Per lo sviluppo locale, usare i user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Database=middifranchi;Username=...;Password=..." \
  --project src/MiddiFranchi.Web
```

Non inserire password, token o connection string in file tracciati.

## Migrazioni EF Core

Il contesto EF Core e le migrazioni si trovano in
`src/MiddiFranchi.Infrastructure/Persistence`. La factory di design-time legge la
connection string dalla variabile d'ambiente `ConnectionStrings__DefaultConnection`.

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Database=middifranchi;Username=...;Password=..."
dotnet ef migrations add <NomeMigrazione> --project src/MiddiFranchi.Infrastructure --startup-project src/MiddiFranchi.Web --output-dir Persistence/Migrations
dotnet ef database update --project src/MiddiFranchi.Infrastructure --startup-project src/MiddiFranchi.Web
```

Le migrazioni sono incluse nel repository. Un database nuovo riceve solo i dati di base
(conti speciali e categorie), non movimenti o strumenti dimostrativi.

## Documentazione

- [Modello di dominio](docs/domain-model.md)
- [Design system](docs/design-system.md)
- [Backup e ripristino](docs/backup-restore.md)
- [Importazione dati](docs/data-import.md)
- [Importazione conti](docs/data-import-accounts.md)
- [Docker e deploy](docs/deployment.md)
- [Docker locale](docs/local-docker.md)
- [Decisioni architetturali](docs/decisions/)

## Nota finale

Questo repository è un esperimento educativo, non un servizio. Se qualcosa non funziona,
la risposta è: studia il codice, prova a sistemarlo e impara. Non è previsto che qualcuno
lo faccia per te.
