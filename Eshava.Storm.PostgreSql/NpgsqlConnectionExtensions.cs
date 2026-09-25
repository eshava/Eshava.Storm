using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eshava.Storm.PostgreSql.Engines;
using Npgsql;

namespace Eshava.Storm
{
	public static class NpgsqlConnectionExtensions
	{
		/// <summary>
		/// Inserts the entities through binary COPY, the PostgreSQL counterpart of SqlBulkCopy.
		/// Requires <see cref="Settings.Dialect"/> to be PostgreSQL.
		/// </summary>
		/// <param name="transaction">Optional; COPY runs in the transaction of the connection, which has to be this one</param>
		public static Task BulkInsertAsync<T>(this NpgsqlConnection connection, IEnumerable<T> entitiesToInsert, NpgsqlTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			return new PostgreSqlBulkInsertEngine().BulkInsertAsync(connection, entitiesToInsert, null, transaction, commandTimeout, cancellationToken);
		}

		/// <summary>
		/// Inserts the entities through binary COPY into a table given by name, which is used as it is
		/// </summary>
		public static Task BulkInsertAsync<T>(this NpgsqlConnection connection, IEnumerable<T> entitiesToInsert, string customTableName, NpgsqlTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			return new PostgreSqlBulkInsertEngine().BulkInsertAsync(connection, entitiesToInsert, customTableName, transaction, commandTimeout, cancellationToken);
		}
	}
}