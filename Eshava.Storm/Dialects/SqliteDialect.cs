namespace Eshava.Storm.Dialects
{
	/// <summary>
	/// SQLite accepts the square brackets of SQL Server and reports no schema, so it is written like SQL Server;
	/// the default schema only has to be the same on both sides of the comparison in the object mapper
	/// </summary>
	internal class SqliteDialect : SqlServerDialect
	{
	}
}