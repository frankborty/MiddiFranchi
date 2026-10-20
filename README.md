# MiddiFranchi

MiddiFranchi is a personal finance application built with .NET 10, Blazor Web App (Interactive
Server rendering), and PostgreSQL. The repository contains the solution bootstrap, the domain
model, and the EF Core/PostgreSQL persistence foundation.

## Project structure

```text
MiddiFranchi/
├── src/
│   ├── MiddiFranchi.Domain/          Entities, value objects, and domain rules
│   ├── MiddiFranchi.Application/     Use cases, application services, and interfaces
│   ├── MiddiFranchi.Infrastructure/  EF Core, Npgsql, persistence, external integrations
│   └── MiddiFranchi.Web/             Blazor Web App (Interactive Server)
├── tests/
│   ├── MiddiFranchi.UnitTests/       Domain and application unit tests
│   └── MiddiFranchi.IntegrationTests/ Integration tests, including future PostgreSQL tests
├── docs/
│   └── decisions/                    Architecture decision records
├── MiddiFranchi.sln
├── AGENTS.md
├── .gitignore
└── README.md
```

### Dependency rules

- Domain depends on no other solution project.
- Application depends on Domain only.
- Infrastructure depends on Application and Domain.
- Web depends on Application and Infrastructure.
- UnitTests reference Domain and Application.
- IntegrationTests reference Domain, Application, and Infrastructure.

## Prerequisites

- .NET 10 SDK (10.0.400 or a compatible later version)
- PostgreSQL for running the application against a database.
- Docker Desktop for the PostgreSQL integration tests. The tests use Testcontainers and are skipped
  by default unless explicitly enabled.
- An optional IDE such as Visual Studio or JetBrains Rider.

## Restore, build, and test

Run these commands from the repository root:

```bash
dotnet restore MiddiFranchi.sln
dotnet build MiddiFranchi.sln --configuration Release --no-restore
dotnet test MiddiFranchi.sln --configuration Release --no-build
```

## Configuration and secrets

No connection string or credentials are committed to this repository. The application reads the
connection string from `ConnectionStrings:DefaultConnection`. For local development, store it with
.NET user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=middifranchi;Username=...;Password=..." --project src/MiddiFranchi.Web
```

Do not add connection strings, passwords, or other secrets to `appsettings.json`,
`appsettings.Development.json`, or any other tracked file.

The Web application starts without opening a database connection. The connection string is resolved
when the `DbContext` is first used.

## Persistence and migrations

The EF Core context and migrations live in `src/MiddiFranchi.Infrastructure/Persistence`. The
design-time factory reads the connection string from the environment variable
`ConnectionStrings__DefaultConnection`; set it only in the current shell session:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Database=middifranchi;Username=...;Password=..."
dotnet ef migrations add <MigrationName> --project src/MiddiFranchi.Infrastructure --startup-project src/MiddiFranchi.Web --configuration Release --output-dir Persistence/Migrations
dotnet ef database update --project src/MiddiFranchi.Infrastructure --startup-project src/MiddiFranchi.Web --configuration Release
```

The committed migration contains schema only and no personal or financial data.

## PostgreSQL integration tests

Integration tests use a real PostgreSQL instance started by Testcontainers. Docker must be running.
They are opt-in so the default test run succeeds without Docker:

```powershell
$env:MIGDIFRANCHI_POSTGRES_TESTS = "1"
dotnet test tests/MiddiFranchi.IntegrationTests/MiddiFranchi.IntegrationTests.csproj --configuration Release
```

The suite creates an isolated container, applies the migrations, and rolls back the data changes of
each test.

## Documentation

Architecture decision records belong in `docs/decisions/`. See
[docs/decisions/README.md](docs/decisions/README.md) for the naming convention.
