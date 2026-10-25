# Deploy con Docker e Portainer

Questa guida descrive il deploy dell'applicazione su un server locale con Portainer, usando un
PostgreSQL esterno e un Docker registry.

Il pacchetto è l'immagine Docker `MiddiFranchi.Web`. Il database non è incluso nello stack: l'app
si collega al PostgreSQL che hai già sul server.

## File aggiunti

- `Dockerfile`: build multi-stage (.NET 10 SDK → runtime ASP.NET Core).
- `.dockerignore`: esclude test, reference e output di build dal contesto.
- `compose.yaml`: stack per Portainer/Docker Compose con volumi e variabili.
- `.env.example`: esempio di variabili per `docker compose` locale.
- `docs/deployment.md`: questa guida.

## 1. Prerequisiti

- Docker Engine sul server con Portainer.
- Un Docker registry raggiungibile dal server (es. `registry.example.com:5000`).
- PostgreSQL raggiungibile dal container dell'app.
- Un database e un utente applicativo.
- Possibilmente un reverse proxy con HTTPS e WebSocket per l'accesso da browser.

L'immagine finale usa `mcr.microsoft.com/dotnet/aspnet:10.0` e ascolta sulla porta **8080**.

Se il registry espone anche una UI web, l'URL della UI non è l'endpoint Docker: usa l'indirizzo
del Docker Registry, quello che risponde su `/v2/`.

## 2. Build e push dell'immagine

Esegui i comandi dalla root del repository, su una macchina con Docker e accesso al registry:

```bash
export REGISTRY=registry.example.com:5000
docker build -t "$REGISTRY/middifranchi:1.0.0" .
docker push "$REGISTRY/middifranchi:1.0.0"
```

Usa un tag immutabile (versione o commit) invece di `latest` per i deploy futuri.

Se il server è `linux/amd64` e stai buildando da un'altra architettura:

```bash
docker buildx build \
  --platform linux/amd64 \
  -t "$REGISTRY/middifranchi:1.0.0" \
  --push .
```

### Registry HTTP / certificato self-signed

Se il registry non usa HTTPS trusted, il Docker daemon della macchina di build **e** del server
Portainer devono considerarlo sicuro. Su Linux, in `/etc/docker/daemon.json`:

```json
{
  "insecure-registries": ["registry.example.com:5000"]
}
```

Sostituisci l'esempio con l'endpoint del tuo registry. Se il registry è raggiungibile tramite un
nome alternativo, usa lo stesso nome nello stack.

Poi riavvia Docker:

```bash
sudo systemctl restart docker
```

Su Docker Desktop: Settings → Docker Engine, aggiungi la stessa voce e applica.

Se il registry richiede autenticazione, aggiungilo in Portainer in **Registries → Add registry**
(tipo Custom registry, URL `registry.example.com:5000`).

## 3. Preparare PostgreSQL

Crea database e utente (esempio):

```sql
CREATE ROLE middifranchi LOGIN PASSWORD 'una-password-forte';
CREATE DATABASE middifranchi OWNER middifranchi;
```

L'utente deve poter creare/alterare tabelle se vuoi che l'app applichi le migrazioni all'avvio.
Se l'utente non ha permessi DDL, imposta `DB_MIGRATE_ON_STARTUP=false` e gestisci lo schema a parte.

Un database nuovo creato dalle migrazioni contiene i conti speciali Cash/Prestiti e le categorie
di base, ma **non** movimenti, strumenti o quotazioni dimostrative. Puoi iniziare subito con i
tuoi dati da **Configurazione → Dati e backup**.

### Connection string

L'app legge `ConnectionStrings:DefaultConnection`; in Docker la variabile è:

```text
ConnectionStrings__DefaultConnection
```

Lo stack la valorizza dalla variabile `DB_CONNECTION_STRING`.

Esempi:

```text
# PostgreSQL installato sull'host Docker
Host=host.docker.internal;Port=5432;Database=middifranchi;Username=middifranchi;Password=...

# PostgreSQL in un container sulla stessa rete Docker
Host=middifranchi-db;Port=5432;Database=middifranchi;Username=middifranchi;Password=...

# PostgreSQL su un'altra macchina
Host=db.example.com;Port=5432;Database=middifranchi;Username=middifranchi;Password=...
```

Per SSL:

```text
...;SSL Mode=Require;Trust Server Certificate=true
```

`Trust Server Certificate=true` va bene solo per un certificato self-signed; in produzione usa un
certificato verificabile.

### PostgreSQL installato sull'host

Il container raggiunge l'host con `host.docker.internal` grazie a `extra_hosts` in `compose.yaml`.
Sul server PostgreSQL devono essere coerenti:

- `listen_addresses` deve includere l'interfaccia raggiungibile dal Docker bridge (spesso `'*'`).
- `pg_hba.conf` deve consentire l'utente dal subnet Docker, ad esempio `172.16.0.0/12` con
  `scram-sha-256`.
- Il firewall dell'host deve consentire la porta 5432 dal subnet Docker.

In alternativa usa l'IP LAN del server nella connection string.

### PostgreSQL in un container Portainer

Se il DB non pubblica la porta sull'host, metti l'app sulla stessa rete Docker del DB:

1. Individua la rete con `docker network ls` (spesso è la rete dello stack del DB).
2. In `compose.yaml` decommenta `networks` e `networks: middifranchi` sotto il servizio, impostando
   `name:` sulla rete reale.
3. Usa il nome del container/servizio PostgreSQL come `Host`.

Se il DB pubblica già `5432` sull'host, puoi semplicemente usare `host.docker.internal`.

## 4. Creare lo stack in Portainer

1. Apri Portainer → **Stacks → Add stack**.
2. Nome: `middifranchi`.
3. Build method: **Web editor**.
4. Incolla il contenuto di `compose.yaml`.
5. Nella sezione **Environment variables** imposta:

| Variabile | Esempio | Note |
|---|---|---|
| `MIDDIFRANCHI_IMAGE` | `registry.example.com:5000/middifranchi:1.0.0` | immagine pubblicata |
| `MIDDIFRANCHI_PORT` | `8080` | usa `127.0.0.1:8080` per non esporre in LAN |
| `DB_CONNECTION_STRING` | `Host=host.docker.internal;Port=5432;Database=middifranchi;Username=middifranchi;Password=...` | segreto |
| `DB_MIGRATE_ON_STARTUP` | `true` | `false` se gestisci lo schema a parte |

6. **Deploy the stack**.
7. Controlla i log del container. All'avvio devono comparire:

```text
Applicazione delle migrazioni del database...
Database aggiornato.
Now listening on: http://[::]:8080
```

8. Apri `http://IP_DEL_SERVER:8080` (o l'URL del reverse proxy).

Se `DB_MIGRATE_ON_STARTUP=true` e il DB non è raggiungibile, il container esce e viene riavviato
da `restart: unless-stopped`: correggi la connection string o i permessi e riavvia lo stack.

## 5. Database esistente e migrazioni

L'app applica le migrazioni EF all'avvio per impostazione predefinita
(`Database:MigrateOnStartup=true`). Questo funziona se il database è gestito da EF e contiene la
tabella `__EFMigrationsHistory`.

Prima del primo deploy fai un backup:

```bash
# Esempio con PostgreSQL in un container
docker exec -t <container-postgres> \
  pg_dump -U <utente> -Fc <database> > middifranchi-before-docker.dump
```

Se il database è stato creato manualmente, non ha `__EFMigrationsHistory`, oppure ha uno schema
diverso:

- non attivare le migrazioni automatiche (`DB_MIGRATE_ON_STARTUP=false`);
- oppure, preferibilmente, crea un database nuovo, lascia che l'app applichi le migrazioni e
  trasferisci i dati con **Configurazione → Dati e backup** (backup completo dal vecchio ambiente,
  ripristino nel nuovo).

Le migrazioni non sono automaticamente reversibili: un rollback dell'immagine non annulla un
cambio di schema. Conserva un backup prima di ogni aggiornamento.

## 6. Volumi e dati

Lo stack crea due volumi:

- `middifranchi-dataprotection`: chiavi Data Protection (antiforgery e circuiti Blazor Server).
- `middifranchi-appdata`: `price-update.json` con impostazioni/stato dell'aggiornamento prezzi.

I dati finanziari sono nel PostgreSQL esterno. I backup dell'app si scaricano dal browser e non
occupano spazio nel container.

Se usi un bind mount al posto del volume, assicurati che la directory sia scrivibile dall'utente
`app` (UID 1654).

## 7. HTTPS, WebSocket e sicurezza

L'app ascolta HTTP sulla 8080. Per l'accesso da browser mettila dietro un reverse proxy
(Nginx Proxy Manager, Traefik, Caddy, ecc.) con:

- inoltro verso `middifranchi:8080` (stessa rete Docker) o `host.docker.internal:8080`;
- **WebSocket abilitati** (Blazor Server ne ha bisogno);
- HTTPS con certificato valido;
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (già impostato nello stack).

L'applicazione **non ha autenticazione integrata**. Non esporre la porta 8080 direttamente su
Internet: usa una VPN o l'autenticazione del reverse proxy. Se la usi solo in locale, limita la
porta a `127.0.0.1:8080` e accedi tramite proxy/VPN.

## 8. Aggiornamenti e rollback

1. Fai un backup completo dal database (e, se vuoi, dall'app).
2. Build e push di un nuovo tag, ad esempio `1.0.1`.
3. In Portainer modifica `MIDDIFRANCHI_IMAGE` con il nuovo tag e fai **Update the stack**.
4. Controlla i log delle migrazioni.

Per rollback, rimetti il tag precedente e ridistribuisci. Se la nuova versione ha applicato
migrazioni non compatibili, ripristina anche il backup del database.

## 9. Risoluzione problemi

| Sintomo | Controllo |
|---|---|
| `Connection refused` verso PostgreSQL | host sbagliato, DB non in ascolto, rete Docker non condivisa, firewall |
| `password authentication failed` | credenziali, `pg_hba.conf`, metodo `scram-sha-256` |
| `relation ... does not exist` | migrazioni non applicate: `DB_MIGRATE_ON_STARTUP=true` o schema allineato |
| Migrazione fallita su tabelle già esistenti | DB senza `__EFMigrationsHistory`: usa un DB nuovo o disattiva le migrazioni |
| Container in restart continuo | `docker logs <container>` / log Portainer; quasi sempre connection string o permessi DB |
| 502 dal reverse proxy | proxy e app non sulla stessa rete/porta; WebSocket non abilitati |
| `host.docker.internal` non risolve | Docker troppo vecchio o `extra_hosts` mancante |
| Pull dell'immagine fallito | registry non raggiungibile, HTTP non trusted, credenziali Portainer |
| `price-update.json` non persiste | volume mancante o permessi della directory per l'utente `app` |

## 10. Comandi rapidi (senza Portainer)

Per provare lo stack sul server:

```bash
cp .env.example .env
# modifica .env con immagine, porta e connection string
docker compose pull
docker compose up -d
docker compose logs -f middifranchi
```

Aggiornamento:

```bash
docker compose pull
docker compose up -d
```
