# Changelog

Notable changes per released version of `Eshava.Storm`, newest first. Versions before 1.0.42 are not
documented here — the Git history is the source for those.

## 1.0.42

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
