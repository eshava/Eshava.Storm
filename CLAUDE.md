# Eshava.Storm — Repository Notes

Object-relational mapper that extends `IDbConnection` with query and execute helpers. Published
as three NuGet packages, **`Eshava.Storm`**, **`Eshava.Storm.Linq`** and **`Eshava.Storm.PostgreSql`**.

**Conventions:** documentation, code and commit messages are written in English. Line endings are
pinned through `.gitattributes` — anything that may run on Linux must be checked out with LF.

## Layout

| Project | Content |
|---|---|
| `Eshava.Storm` | The mapper itself — the `IDbConnection` extension methods, type handlers, mapping. |
| `Eshava.Storm.Linq` | Expression-based query building on top of it. `netstandard2.0`. |
| `Eshava.Storm.PostgreSql` | The bulk insert for PostgreSQL through binary `COPY`. A package of its own because it needs Npgsql, which nobody on SQL Server should have to carry. `net8.0` to `net10.0`, as Npgsql 10. |
| `Eshava.Storm.Tests` · `Eshava.Storm.Linq.Tests` | MSTest with FluentAssertions. |
| `Eshava.Storm.IntegrationTests` | Tests against a real PostgreSQL database — see below. |
| `Eshava.Storm.TestApp` | Manual test application against a real database. |

Note the naming: test projects here are `Eshava.Storm.Tests`, whereas the other repositories use
`Eshava.Test.<Project>`. Follow whichever convention the repository you are in already uses.

## Rules

* **This is the database access layer of `Eshava.DomainDrivenDesign`.** Its infrastructure base
  classes build on `Eshava.Storm` and `Eshava.Storm.Linq`, which is why Entity Framework is not
  used there. A behavioural change in the mapper reaches that repository once the package
  version is raised.
* Public API changes need a test in the matching test project. The test application is for
  manual verification against a real database, not a substitute for one.

## Integration Tests

**`Eshava.Storm.IntegrationTests` runs against PostgreSQL**, because the dialect cannot be proven
without the provider: how the reader reports schemas, how Npgsql types a `DateTime`, what an empty list
has to look like. Everything else is tested without a database, in `Eshava.Storm.Tests` — SQLite in
memory, or a recording connection that keeps the command instead of running it.

**The database comes from the environment variable `STORM_POSTGRESQL_CONNECTION_STRING`**, an Npgsql
connection string. Without it every test is reported as inconclusive, not as failed. The tests create
and drop their own tables and a schema `storm_notes`, so the database should be one of its own. They
run one after the other, since they share it.

```bash
export STORM_POSTGRESQL_CONNECTION_STRING="Host=<host>;Database=<database>;Username=<user>;Password=<password>"
dotnet run --project Eshava.Storm.IntegrationTests/Eshava.Storm.IntegrationTests.csproj
```

The test projects use `MSTest.Sdk`, which runs through `dotnet run`; `dotnet test` fails on the .NET 10
SDK without the new test experience configured.

## Dependencies

`Eshava.Storm.Linq` consumes `Eshava.Core.Linq` as a NuGet package.
