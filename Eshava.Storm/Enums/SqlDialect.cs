namespace Eshava.Storm.Enums
{
	/// <summary>
	/// The SQL dialect Storm writes: how names are quoted, which schema a table without one is in,
	/// how a generated key is read back and how some parameter types are passed
	/// </summary>
	public enum SqlDialect
	{
		SqlServer = 0,
		Sqlite = 1,
		PostgreSql = 2
	}
}