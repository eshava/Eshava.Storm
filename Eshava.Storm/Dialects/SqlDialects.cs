using Eshava.Storm.Enums;

namespace Eshava.Storm.Dialects
{
	internal static class SqlDialects
	{
		private static readonly ISqlDialect _sqlServer = new SqlServerDialect();
		private static readonly ISqlDialect _sqlite = new SqliteDialect();
		private static readonly ISqlDialect _postgreSql = new PostgreSqlDialect();

		/// <summary>
		/// The dialect of <see cref="Settings.Dialect"/>
		/// </summary>
		public static ISqlDialect Current => For(Settings.Dialect);

		public static ISqlDialect For(SqlDialect dialect)
		{
			switch (dialect)
			{
				case SqlDialect.Sqlite:
					return _sqlite;
				case SqlDialect.PostgreSql:
					return _postgreSql;
				default:
					return _sqlServer;
			}
		}
	}
}