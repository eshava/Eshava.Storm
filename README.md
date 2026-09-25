# Storm
Eshava.Storm - a simple tentaculated object-relational mapper for .Net

## Features
Eshava.Storm is a [NuGet library](https://nuget.org/packages/Eshava.Storm) that you can add in to your project that will extend your `IDbConnection` interface.

It provides 4 helpers:

Execute a query and map the results to a strongly typed List
------------------------------------------------------------

```csharp
public static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameter = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
```
Example usage:

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
}

var alphas = await connection.QueryAsync<Alpha>("SELECT * FROM alphas WHERE Beta = @Beta", new { Beta = "nonsense" });
```


Execute a query and map the results to a strongly typed List with several sub objects
------------------------------------------------------------

```csharp
public static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection connection, string sql, Func<IObjectMapper, T> map, object parameter = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
```
Example usage:

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
    public Guid OmegaId {get; set;}
    public Omega Omega {get; set;}
}

public class Omega
{
    public string Name { get; set; }
}

var query = $@"
SELECT 
     a.*
    ,o.*
FROM alphas a
LEFT OUTER JOIN omegas o ON o.Id = a.OmegaId
WHERE a.Beta = @Beta";

var alphas = await connection.QueryAsync<Alpha>(
    query, 
    mapper => 
    {
        var alpha = mapper.Map<Alpha>("a");
        var omega = mapper.Map<Omega>("o");
        
        alpha.Omega = omega;
        
        return alpha;
    },
    new { Beta = "nonsense" });
```


Execute a Command that returns no results
-----------------------------------------

```csharp
public static Task<int> ExecuteAsync(this IDbConnection connection, string sql, object parameter = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
```
Example usage:

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
}

await connection.ExecuteAsync("UPDATE alpha SET Beta = @Beta WHERE Id = @Id", new { Beta = "nonsense", Id = Guid.NewGuid() });
```


Execute a Command that returns a scalar
-----------------------------------------

```csharp
public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object parameter = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
```
Example usage:

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
}

await connection.ExecuteAsync("SELECT COUNT(Id) FROM alpha WHERE Beta = @Beta", new { Beta = "nonsense" });
```


Parameterized queries
---------------------

Parameters are passed in as anonymous classes. This allow you to name your parameters easily and gives you the ability to simply cut-and-paste SQL snippets and run them in your db platform's Query analyzer.

```csharp
new { Gamma = 1, Beta = "nonsense" } // Gamma will be mapped to the param @Gamma, Beta to the param @Beta
```

Also supported

```csharp
// Gamma will be mapped to the param @Gamma, Beta to the param @Beta
new List<KeyValuePair<string, object>() 
{ 
    new KeyValuePair<string, object>("Gamma", 1),
    new KeyValuePair<string, object>("Beta", "nonsense")
};
```


List Support
------------

Eshava.Storm allows you to pass in `IEnumerable<int>` and will automatically parameterize your query.

For example:

```csharp
connection.QueryAsync<Guid>("SELECT Id FROM alphas where Gamma in @Gammas", new { Gammas = new int[] { 3, 5, 7 } });
```

Will be translated to:

```sql
SELECT Id FROM alphas where Gamma in (@Gammas_p0, @Gammas_p1, @Gammas_p2) -- @Gammas_p0 = 3 , @Gammas_p1 = 5 , @Gammas_p2 = 7
```

An empty list becomes an empty set, `(SELECT NULL WHERE 1 = 0)`: `IN` matches no row and `NOT IN`
matches every row. Each element whose type has a registered type handler is set through that
handler. A `byte[]` is a single binary value, not a list.


Interpolated SQL
----------------

On .NET 6 and later a statement can be written as an interpolated string. A hole directly preceded
by `@` becomes a parameter; any other hole is inserted as text and is meant for identifiers only.

```csharp
SqlInterpolatedStringHandler query = $"SELECT * FROM {TypeAnalyzer.GetTableName<Alpha>()} WHERE Beta = @{beta}";
var alphas = await connection.QueryAsync<Alpha>(ref query);
```

**Never put a value into a hole without `@`.** It is not escaped, so a value that comes from outside
the application opens the statement to SQL injection.


Owns One Property Support (entity framework behavior)
------------

Eshava.Storm allows you to fill sub objects which are marked as owns one properties.

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
    
    [OwnsOne]
    public Omega Omega {get; set;}
}

public class Omega
{
    public string Name { get; set; }
}
```

Sql query example:

```sql
SELECT * FROM alphas
```

OR

```sql
SELECT  
     Id
    ,Beta
    ,Gamma
    ,Omega_Name
FROM alphas
```


Custom data type handler
------------

Eshava.Storm allows you to define treatments for custom data types or to change the treatment of known data types

```csharp
public interface ITypeHandler
{
    bool ReadAsByteArray { get; }

    void SetValue(IDbDataParameter parameter, object value);
    object Parse(Type destinationType, object value);
}
```

OR

```csharp
public abstract class TypeHandler<T> : ITypeHandler
{
    public abstract void SetValue(IDbDataParameter parameter, T value);
    public abstract T Parse(object value);
}
```

For example:

```csharp
public class DateTimeHandler : TypeHandler<DateTime>
{
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = value.ToUniversalTime();
    }

    public override DateTime Parse(object value)
    {
        return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
    }
}
```


## CRUD Operations
Eshava.Storm provides also helpers to simple create, update and delete data objects


Query data
------------

The query method allows you to read a data object by id.
If the data object has an combined key, you can pass an anonymous type object as id parameter.

```csharp
public static Task<T> QueryEntityAsync<T>(this IDbConnection connection, object id, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```


Insert data
------------

The insert method allows you to define the type of the primary key column

```csharp
public static Task<K> InsertAsync<T, K>(this IDbConnection connection, T entityToInsert, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```


Update data
------------

```csharp
public static async Task<bool> UpdateAsync<T>(this IDbConnection connection, T entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```


Partial update data
------------

The partial update method analyzes the given data type and passed entity, so that only an anonymous object can be parsed

```csharp
    public static async Task<bool> UpdatePartialAsync<T>(this IDbConnection connection, object entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```

For Example:

```csharp
public class Alpha
{
    public Guid? Id { get; set; }
    public string Beta { get; set; }
    public int Gamma { get; set; }
}

var partialAlpha = new 
{
    Id = Guid.NewGuid(),
    Beta = "nonsense"
};

await connection.UpdatePartialAsync<Alpha>(partialAlpha);
```


Delete data
------------

```csharp
public static async Task<bool> DeleteAsync<T>(this IDbConnection connection, T entityToDelete, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```


Bulk Insert data
------------

```csharp
public static Task BulkInsertAsync<T>(this SqlConnection connection, IEnumerable<T> entitiesToInsert, SqlTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```

```csharp
public static Task BulkInsertAsync<T>(this SqlConnection connection, IEnumerable<T> entitiesToInsert, string CustomTableName, SqlTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
```

## Configuration via attributes

* `KeyAttribute`
* `NotMappedAttribute`
* `ReadOnlyAttribute`
* `DatabaseGeneratedAttribute`
* `ColumnAttribute`
* `TableAttribute`

There is also a custom attribute to support the entity framework behaviour with owns one objects

* `OwnsOneAttribute`

None of these attributes have to be set.

* For primary key columns, the property name `Id` will be used as fallback
* Table name will be the pluralized data type name 
* As default, the value of primary key columns are treated as manually generated and will be added to the insert query

The attribute `DatabaseGeneratedAttribute` allows you also to mark properties as identity column (need not to be a primary column).

Register (optional):
```csharp
Eshava.Storm.MetaData.TypeAnalyzer.AddType<Alpha>();
```

## Configuration via fluid api pattern

Instead of defining the configuration of the classes via attributes, it is possible to control them via an EntityTypeConfiguration
The configuration is based on the behaviour of the Entity Framework Code.

Define:
```csharp
public class AlphaTypeConfiguration : IEntityTypeConfiguration<Alpha>
{
    public void Configure(EntityTypeBuilder<Alpha> builder)
    {
	    ...
    }
}
```

Register:
```csharp
Eshava.Storm.MetaData.TypeAnalyzer.AddType(new AlphaTypeConfiguration());
```

## Global settings
```csharp
/// <summary>
/// Specifies the default Command Timeout for all Queries
/// </summary>
Eshava.Storm.Settings.CommandTimeout = null;

/// <summary>
/// If a column name is duplicated, the duplicates are skipped (by default, all are processed, so the last column wins)
/// Hint: Will be ignored if an table alias is passed for object mapping 
/// </summary>
/// <remarks>This setting should be set once at the start of the application; it is not intended to be toggled per-query</remarks>
Eshava.Storm.Settings.IgnoreDuplicatedColumns = false;

/// <summary>
/// If the DatabaseGenerated attribute is not set, the option specifies whether the value of a key column is assumed to be autogenerated.
/// </summary>
Eshava.Storm.Settings.DefaultKeyColumnValueGeneration = DatabaseGeneratedOption.None;

/// <summary>
/// Date and time data. Date value with an accuracy of 100 nanoseconds.
/// </summary>
Eshava.Storm.Settings.EnableDateTimeHighAccuracy = false;

/// <summary>
/// All models must be explicitly registered by a DbConfiguration or by TypeAnalyzer.Register<>() to be used in the mapper.
/// </summary>
Eshava.Storm.Settings.RestrictToRegisteredModels = false;

/// <summary>
/// The SQL dialect Storm writes: SqlServer (default), Sqlite or PostgreSql.
/// </summary>
Eshava.Storm.Settings.Dialect = SqlDialect.SqlServer;
```

## Dialects

Storm writes SQL Server by default. Set the dialect once at the start of the application; it is global,
because table names are also written into hand-written SQL, where no connection is known.

```csharp
Eshava.Storm.Settings.Dialect = SqlDialect.PostgreSql;
```

| | SQL Server | SQLite | PostgreSQL |
|---|---|---|---|
| Names | `[Items].[Name]` | like SQL Server | `"items"."name"` — lower case, quoted |
| Table without schema | in `dbo` | — | in `public` |
| Generated key | `SCOPE_IDENTITY()` | `last_insert_rowid()` | `INSERT … RETURNING` |
| Empty list | `(SELECT NULL WHERE 1 = 0)` | like SQL Server | an empty array of the element type |
| `DateTime` parameter | `DateTime` or `DateTime2` | like SQL Server | chosen by Npgsql from the kind: `timestamptz` for UTC |
| `DateTimeOffset` parameter | as it is | as it is | converted to UTC — `timestamptz` stores the instant, not the offset |

**PostgreSQL names are written in lower case.** PostgreSQL folds a name without quotes to lower case, so
this matches tables created without quotes as well as hand-written SQL that does not quote, and still
protects a name that is a reserved word. `TypeAnalyzer.GetTableName<Item>()` returns `"items"`.

### Bulk insert on PostgreSQL

The package [Eshava.Storm.PostgreSql](https://nuget.org/packages/Eshava.Storm.PostgreSql) adds
`BulkInsertAsync` for `NpgsqlConnection`, the counterpart of the one for `SqlConnection`. It writes
through binary `COPY` and needs the PostgreSQL dialect.

```csharp
Eshava.Storm.Settings.Dialect = SqlDialect.PostgreSql;

await connection.BulkInsertAsync(shipments);
await connection.BulkInsertAsync(shipments, "shipments_archive", transaction);
```

Binary `COPY` converts nothing on the server, so each value is written as the type of its column, which
is read from the table first. A `DateTime` without kind goes into `timestamptz` as UTC.

The command engine for insert, update and delete follows the connection — `SqlConnection`, a SQLite or
an Npgsql connection, also inside a wrapping connection — and does not depend on the setting.

# Storm.Linq
Eshava.Storm.Linq - a extension to Eshava.Storm

## Features
Eshava.Storm.Linq is a [NuGet library](https://nuget.org/packages/Eshava.Storm.Linq) that you can add in to your project that will extend your `IEnumerable` interface.

It provides 5 helpers:

Converts a list of linq filter expressions to plain sql
------------------------------------------------------------
The linq filter expressions can be created manually or with the `WhereQueryEngine` from Eshava.Core.Linq is a [NuGet library](https://nuget.org/packages/Eshava.Core.Linq)

```csharp
public static WhereQueryResult AddWhereConditionsToQuery<T>(this IEnumerable<Expression<Func<T, bool>>> queryConditions, string sqlQuery, WhereQuerySettings settings = null) where T : class
```
```csharp
public static WhereQueryResult CalculateWhereConditions<T>(this IEnumerable<Expression<Func<T, bool>>> queryConditions, WhereQuerySettings settings) where T : class
```
The extension methods produces an `WhereQueryResult` that contains the raw sql query and a dictionary of parameter
```csharp
public class WhereQueryResult
{
	public string Sql { get; set; }
	public Dictionary<string, object> QueryParameter { get; set; }
}
```

Converts a list of order by conditions to plain sql
------------------------------------------------------------
The order by conditions can be created with the `SortingQueryEngine` from Eshava.Core.Linq is a [NuGet library](https://nuget.org/packages/Eshava.Core.Linq)
```csharp
public static string AddSortConditionsToQuery(this IEnumerable<OrderByCondition> orderByConditions, string sqlQuery, QuerySettings settings = null)
```
```csharp
public static string CalculateSortConditions(this IEnumerable<OrderByCondition> orderByConditions, QuerySettings settings = null)
```

Appends skip and take to a sql query
------------------------------------------------------------
```csharp
public static string AppendSkipAndTake(this string sqlQuery, int skip, int take)
```

Recommended settings
------------------------------------------------------------
For the four extension method it is recommended to pass property name mappings. This can be achieved in two different ways, explicit or implicit. 
```csharp
public class QuerySettings
{
	public Dictionary<string, string> PropertyMappings { get; set; }
	public Dictionary<Type, string> PropertyTypeMappings { get; set; }
}
	
public class WhereQuerySettings : QuerySettings
{
	public Dictionary<string, object> QueryParameter { get; set; }
}
```

##### Example usage:

The Alpha and Omega classes each represent a table and are joined in the example and have the aliases "a" and "o  
A possible member expression could be `p => p.Omega.Psi` und should be translated to `o.Psi`. Therefore a mapping must be created.

```csharp
public class Alpha
{
    public string Beta { get; set; }
    public Omega Omega { get; set;}
}

public class Omega
{
    public string Psi { get; set; }    
}
```

##### Explicit way
During processing the expression `p => p.Omega.Psi` becomes the string `.Omega.Psi`.
So that the mapping looks like:
```csharp
settings.PropertyMappings.Add(".Omega.Psi","o.Psi");
```

##### Implicit way
During processing the expression `p => p.Omega.Psi` becomes the string `.Omega.Psi`.
So that the mapping looks like:
```csharp
settings.PropertyTypeMappings.Add(typeof(Omega),"o");
```

Comming soon
------------------------------------------------------------
In a later stage there will be a intelligent processing of the member expressions.
For processing, the DbConfigurations shall be extended by navigation relations, so that Joins and OwnsOnes can be recognized and translated automatically. 
