# Changelog

Notable changes per released version of `Eshava.Storm`, newest first. Versions before 1.0.42 are not
documented here — the Git history is the source for those.

## 1.0.42

### Changed

* **`DateTimeOffset`, `DateOnly` and `TimeOnly` properties are mapped.** They were missing from the
  list of simple types, so a property of one of these types was left out of every `INSERT` and
  `UPDATE` and never read — the column stayed `NULL` and the property came back as its default,
  without any error. `DateOnly` and `TimeOnly` were only mapped when a type handler was registered
  for them.

  **Check your entities before updating:** such a property is now part of the generated `INSERT`
  and `UPDATE`, so a table without the matching column makes the statement fail. Mark a property
  that has no column with `[NotMapped]`.
* **A single `byte[]` is one binary parameter.** Without a type handler it used to be expanded like a
  list, into one parameter per byte. A list of `byte[]` becomes one binary parameter per element.
* **A type handler wins over the built-in conversions when reading**, as it already did when
  writing. An enum with a handler that stores it as text used to be written through the handler
  and read as a number, which failed.
* **The bulk insert applies the type handler of a simple type**, as `InsertAsync` does through its
  parameters. A registered `DateTime` handler, for instance, was applied by `InsertAsync` and
  skipped by `BulkInsertAsync`, so the two paths stored different values. A handler that does not
  implement `IBulkInsertTypeHandler` gets an untyped column, since only the handler knows the type
  of the value it produces.

### Fixed

* **A registered type handler now applies to the elements of an enumeration parameter.** A list
  parameter such as `WHERE Fingerprint IN @Fingerprints` with `new[] { hashA, hashB }` becomes one
  parameter per element, and each element whose type has a handler is set through that handler —
  the same way a single parameter of that type already was.

  Before, the `DbType` of the list was computed once and handed to every element, and since the
  handler lookup only runs for a parameter without a `DbType`, the handler of the element type was
  never found. With a `TypeHandler<byte[]>` registered, a list of `byte[]` expanded every element a
  second time and the command failed with *Must add values for the following parameters*. A list of
  a custom type with a handler that is not a known database type failed earlier still, with a
  `NotSupportedException` from the type lookup.

  Lists of plain types — `int[]`, `string[]`, a `List<Guid>` — are unchanged: their elements keep
  the `DbType` of the list, as before.

* **The analysis of a type is published only once it is complete.** It was put into the cache
  first and filled afterwards, so requests using a type for the first time in parallel could read
  a half analysed type: an `INSERT` without columns, a missing key, objects mapped only partly, or
  a *Collection was modified* exception. The registry of type handlers had the same kind of race
  when it was first used.
* **Registering a type handler before the first query no longer loses the built-in handlers** for
  `DataTable` and `IEnumerable<SqlDataRecord>`. They are also found for a class derived from
  `DataTable` and for any implementation of `IEnumerable<SqlDataRecord>`, such as a
  `List<SqlDataRecord>`, which used to fail with a `NotSupportedException`.
* **`Settings.RestrictToRegisteredModels` applies to insert, update, delete, query by id and bulk
  insert as well.** Those paths analysed an unregistered type silently.
* **Reading converts the values a provider stores as text or bytes:** a `Guid` from text or bytes,
  a `TimeSpan` and a `DateTimeOffset` from text, a `DateOnly` from a `DateTime` or text, a
  `TimeOnly` from a `TimeSpan`, a `DateTime` or text. These failed with an `InvalidCastException`.
* **An enum based on `long` keeps its full range when read**, instead of overflowing through
  `Int32`. A list of nullable enums can be used as a parameter; it failed with a
  `NotSupportedException`.
* **A type handler that reads a byte array parses the value once.** It was parsed a second time on
  its own result, which failed with an `InvalidCastException`.
* **An owned type configured through `OwnsOne` with only some of its properties keeps the others.**
  Configuring one property of the owned type used to drop every property that was not configured.
* **Class mapping works on SQLite.** The object mapper read the `IsHidden` column of the schema
  table, which Microsoft.Data.Sqlite does not provide, so every query mapped onto a class failed.
