# Eshava.Storm — Repository Notes

Object-relational mapper that extends `IDbConnection` with query and execute helpers. Published
as two NuGet packages, **`Eshava.Storm`** and **`Eshava.Storm.Linq`**.

**Conventions:** documentation, code and commit messages are written in English. Line endings are
pinned through `.gitattributes` — anything that may run on Linux must be checked out with LF.

## Layout

| Project | Content |
|---|---|
| `Eshava.Storm` | The mapper itself — the `IDbConnection` extension methods, type handlers, mapping. |
| `Eshava.Storm.Linq` | Expression-based query building on top of it. `netstandard2.0`. |
| `Eshava.Storm.Tests` · `Eshava.Storm.Linq.Tests` | MSTest with FluentAssertions. |
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

## Dependencies

`Eshava.Storm.Linq` consumes `Eshava.Core.Linq` as a NuGet package.
