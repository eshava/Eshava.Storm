# Changelog

Notable changes per released version of the two packages of this repository, `Eshava.Storm` and
`Eshava.Storm.Linq`, newest first. Versions before `Eshava.Storm` 1.0.42 and `Eshava.Storm.Linq` 1.0.15
are not documented here — the Git history is the source for those.

## Eshava.Storm.Linq 1.0.15

### Changed

* **New conditions are inserted where the `WHERE` clause ends**, before a `GROUP BY`, `HAVING`,
  `ORDER BY`, `OFFSET`, `OPTION` or `FOR XML`/`FOR JSON` of the base query. They were appended at the
  end of the query, behind such a clause, which produced invalid SQL. A query that ends with its
  `WHERE` clause is extended exactly as before.
* **An existing `WHERE` condition with a top-level `OR` is enclosed in parentheses** before the new
  conditions are added. `WHERE IsDeleted = 0 OR IsArchived = 0` plus `x => x.TenantId == 1` became
  `... OR IsArchived = 0 AND (TenantId = @p0)`, and since `AND` binds tighter than `OR` the tenant
  condition applied to the second half only — rows of other tenants were returned.
* **Every part of a condition that does not depend on the lambda parameter is evaluated once and
  passed as a parameter**: a captured variable, a member of a captured object at any depth, a static
  member such as `Guid.Empty` or `string.Empty`, and a call such as `DateTime.UtcNow.AddDays(-1)`.
  A captured `bool` combined with `&&` or `||` becomes `(1 = 1)` or `(1 = 0)`.
* **What cannot be translated throws a `NotSupportedException`** instead of producing wrong or
  invalid SQL: a static method applied to a column, such as `Math.Abs(x.Balance)`, which was dropped
  silently and compared the column itself; arithmetic on a column; a column searched for inside a
  captured string, `search.Contains(x.Name)`; and `Any` over a column instead of a list of values.
* **Wildcards in a search term are escaped.** `%`, `_` and `[` in the value of `Contains`,
  `StartsWith` and `EndsWith` are matched as themselves, using the bracket syntax of SQL Server.
  `Contains("_")` matched every row.
* **The query parameters of the settings are copied, not changed.** The result holds them together
  with the new parameters, as before, but the settings can be used for a second query, which failed
  with a duplicate key, and generated names skip every name the given parameters already use.

### Fixed

* **`WHERE` and `ORDER BY` are recognised as keywords of the statement itself**: as whole words,
  outside parentheses, string literals and quoted names. `SELECT SomewhereId FROM Items` got an
  `AND` instead of a `WHERE`, and a `WHERE` of a subquery inside the `WHERE` clause made the query get
  a second one.
* **A captured object whose type is mapped in `PropertyTypeMappings` is a value, not a table.**
  `x => x.Name == request.Model.Name` became `(i.Name = i.Name)` and returned nearly every row.
* **`null` reached through a captured member is a test for `NULL`.** `x => x.Name == filter.Name`
  with `filter.Name` being `null` became `Name = @p0` with a `NULL` parameter, which matches nothing.
* **Both sides of a comparison are translated**: a column compared with a column, `r.Start < r.End`,
  and a value or `null` on the left, `5 < r.Id` or `null == r.Name`. These produced invalid SQL.
* **An empty list matches nothing**: `empty.Contains(x.Id)` and `empty.Any(...)` become `(1 = 0)`.
  They produced invalid SQL.
* **`Any` can use members of the item and pass the item to a method**, as in
  `items.Any(i => i.Id == x.Id)` and `names.Any(n => x.Name.StartsWith(n))`. The first passed the
  items themselves as values, the second failed with a `KeyNotFoundException`.
* **A search term that is `null` throws an `ArgumentNullException`**, as `String.StartsWith(null)`
  does, instead of a `NullReferenceException` from inside the translation.
* **An enum constant based on `long` keeps its full range**, instead of overflowing through `Int32`.

## Eshava.Storm 1.0.42

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
* **A parameter set through a type handler no longer has its `DbType` forced to `Object`** before the
  handler runs. On SQL Server that became `sql_variant` whenever the handler only set the value,
  which fails for values over 8000 bytes and keeps an index on the compared column from being used.
  The type is now the one the handler sets, or the one the provider infers from the value.
* **Owned objects nested in owned objects are read from the columns they are written to.** Reading
  dropped the outer prefix — `Address.Geo.Lat` was written to `Address_Geo_Lat` and read from
  `Geo_Lat` — and took the prefix from the column name of the owned property where writing takes
  its property name. Both now use the property names, as writing always did.
* **`Settings.IgnoreDuplicatedColumns` applies to readers with a column schema too**, which includes
  SqlClient and SQLite. It only worked for readers without one.
* **`QueryFirstOrDefaultAsync` stops after the first row** instead of reading the whole result.
* **Mapping is planned once per result instead of once per row.** Which column goes into which
  property, including owned objects, is worked out for the first row and reused; the analysis of a
  statement text is cached, and names are compared culture invariantly.
* **A partial update or patch has to give every key column.** With a composite key such as
  `(TenantId, Id)`, `UpdatePartialAsync<T>(new { Id = 5, Name = "x" })` updated the row with `Id` 5
  in every tenant, and reported `false` although it had changed them. It now throws an
  `ArgumentException` before anything is sent.
* **An update without a column to set throws an `ArgumentException`**: a partial update that gives
  only key columns, or a property name the entity does not have. It used to send `UPDATE ... SET
  WHERE`, which failed with a syntax error.
* **An owned object that is not set writes its columns as `NULL`.** An update with an owned object
  set to `null` left its columns unchanged and reported success; an insert left them out.
* **Update and delete report success when at least one row changed**, instead of exactly one. An
  update through a list of keys changes several rows and reported `false`, and so did a table with
  a trigger that changes rows as well.
* **SQLite is recognised by the type name and namespace of the connection, and through a connection
  that wraps it** in a `WrappedConnection`, `InnerConnection` or `UnderlyingConnection` property, as
  profilers do. Every other connection is still treated as SQL Server.
* **The bulk insert closes a connection it opened**, as every other command does, disposes its
  `SqlBulkCopy`, and applies `Settings.CommandTimeout` when no timeout is given.

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
* **A list parameter replaces its own name only.** The expansion replaced `@Name` as plain text, so
  `@Id` also rewrote `@IdName`, a list `@Id` corrupted an already expanded `@Ids`, and in an
  interpolated statement with ten or more parameters a list in the first hole rewrote the tenth.
  All of these failed with a syntax error.
* **An empty list is an empty set**, `(SELECT NULL WHERE 1 = 0)`. It used to leave `IN @Name` in the
  statement without a parameter, which failed.
* **A list passed with a key that starts with `@`**, such as `"@Ids"` in a list of key value pairs,
  is expanded. It was left in the statement unreplaced.
* **A statement without `FROM` can be mapped onto a class**, such as `SELECT 1 AS Id` or a stored
  procedure call. The mapper searched the select list up to `" from "` and failed with an
  `ArgumentOutOfRangeException` when there was none.
* **A computed column is mapped when a table alias is requested.** `(SELECT COUNT(*) ...) AS Total`
  in a query mapped with `Map<T>("i")` stayed at its default.
* **An alias is recognised as a whole.** The alias `e` was also found in `le.`, which put the aliases
  of a query in the wrong order and left values empty.
* **A column is assigned to its table alias from the select list of the outermost statement**, and a
  use under another name (`e.Name AS Label`) counts for the renamed column only. The select list used
  to end at the first `" from "`, which is inside a subquery of the select list or inside a common
  table expression, and a column at its end or renamed with `AS` was not recognised.
* **Common table expressions, comma separated tables and derived tables in `FROM` can be mapped by
  their alias**: `WITH Selected AS (...) SELECT s.* FROM Selected s`, `FROM Items a, Items b` and
  `FROM (SELECT ...) x`. A derived table in a `JOIN` is mapped through the tables it reads from.
  All of these returned empty objects.
* **The same alias for derived tables in several parts of a statement**, as in a `UNION`, no longer
  fails with *An item with the same key has already been added*.
* **A table name can be used as alias** when the table has an alias in the statement. The comparison
  never matched, because table names carry their schema.
* **A reader without a column schema is supported.** Such a reader throws from `GetSchemaTable`, which
  failed the query; the column types reported by `GetDataType` are now taken from the reader.
* **A patch with a key property renamed through `[Column]`** no longer fails with *Sequence contains no
  matching element*: the patch names its values by property, the lookup used the column name.
* **Column names are quoted in the `WHERE` clause** of update, delete and query by id, as they already
  were in `INSERT` and `SET`. A key column named with a reserved word, `[Column("Order")]`, could be
  inserted but not updated, deleted or read. Parameter names are taken from property names, so a
  column name with a blank no longer produces an invalid parameter.
* **A dictionary with values of another type than `object`**, such as `Dictionary<string, int>`, can be
  passed as parameters. It was taken for a list of parameter sets and refused with *An enumerable
  sequence of parameters is not allowed in this context*.
