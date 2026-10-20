# AGENTS.md

## Purpose

MiddiFranchi is a personal finance application. This repository currently contains only the solution
bootstrap: project structure, dependencies, and tooling. Business features must be added incrementally.

## Technology stack

- C#
- .NET 10 (`net10.0`)
- Blazor Web App with Interactive Server rendering
- Clean Architecture
- Entity Framework Core with PostgreSQL via Npgsql
- xUnit and Shouldly for automated tests

## Project responsibilities and dependency rules

- `src/MiddiFranchi.Domain`: entities, value objects, and domain rules. It must not reference other
  solution projects or infrastructure frameworks.
- `src/MiddiFranchi.Application`: use cases, application services, and interfaces. It may reference
  `MiddiFranchi.Domain` only.
- `src/MiddiFranchi.Infrastructure`: EF Core, Npgsql, persistence implementations, and external
  integrations. It may reference `MiddiFranchi.Application` and `MiddiFranchi.Domain`.
- `src/MiddiFranchi.Web`: Blazor Web App using Interactive Server rendering. It may reference
  `MiddiFranchi.Application` and `MiddiFranchi.Infrastructure`.
- `tests/MiddiFranchi.UnitTests`: domain and application unit tests.
- `tests/MiddiFranchi.IntegrationTests`: integration tests, including future PostgreSQL tests.

Dependencies must point inward toward the Domain. Do not introduce circular project references or
reference infrastructure frameworks from the Domain or Application projects.

## Coding conventions

- Use clear, descriptive names for types, members, and variables.
- Prefer async I/O for operations that touch external resources; do not block with `.Result` or
  `.Wait()`.
- Accept and flow a `CancellationToken` through asynchronous operations.
- Use `decimal` for monetary amounts and financial calculations. Never use `float` or `double` for
  money or financial arithmetic.
- Keep code and public APIs as small as the current requirement allows. Avoid unnecessary
  abstractions and packages.

## Testing expectations

- Use xUnit as the test framework and Shouldly for assertions.
- Write meaningful tests for business logic and non-trivial behavior; avoid placeholder assertions.
- Unit tests must not depend on a live database.
- Integration tests that require PostgreSQL must be opt-in through external configuration and must
  not prevent the default test run from succeeding without a database.

## Security rules

- Never commit connection strings, passwords, API keys, tokens, or other credentials.
- Store local secrets outside the repository. The Web project is configured for .NET user secrets.
- Keep committed configuration files free of environment-specific secrets.

## Change discipline

- Keep changes focused on the requested task and avoid modifying unrelated files.
- Do not silently change the architecture or project dependency rules.
- Do not add business features, database migrations, or extra dependencies unless explicitly requested.
