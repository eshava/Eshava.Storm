using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eshava.Storm.Enums;
using Eshava.Storm.Models;
using Npgsql;

namespace Eshava.Storm.PostgreSql.Engines
{
	internal class PostgreSqlBulkInsertEngine
	{
		public async Task BulkInsertAsync<T>(NpgsqlConnection connection, IEnumerable<T> entities, string customTableName, NpgsqlTransaction transaction, int? commandTimeout, CancellationToken cancellationToken) where T : class
		{
			if (connection == null)
			{
				throw new ArgumentNullException(nameof(connection));
			}

			// Table and column names are quoted in the dialect, and only the PostgreSQL one matches the table
			if (Settings.Dialect != SqlDialect.PostgreSql)
			{
				throw new InvalidOperationException($"The bulk insert for PostgreSQL requires {nameof(Settings)}.{nameof(Settings.Dialect)} to be {nameof(SqlDialect.PostgreSql)}.");
			}

			if (transaction != null && transaction.Connection != connection)
			{
				throw new ArgumentException("The transaction belongs to another connection.", nameof(transaction));
			}

			var rows = BulkInsertRows.Create(entities, () => new NpgsqlParameter(), customTableName);

			var wasClosed = connection.State == ConnectionState.Closed;
			if (wasClosed)
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}

			try
			{
				var columnList = String.Join(", ", rows.Columns.Select(column => column.QuotedName));
				var dataTypeNames = await ReadDataTypeNamesAsync(connection, transaction, rows.TableName, columnList, cancellationToken).ConfigureAwait(false);

				await using var importer = await connection
					.BeginBinaryImportAsync($"COPY {rows.TableName} ({columnList}) FROM STDIN (FORMAT BINARY)", cancellationToken)
					.ConfigureAwait(false);

				var timeout = commandTimeout ?? Settings.CommandTimeout;
				if (timeout.HasValue)
				{
					importer.Timeout = TimeSpan.FromSeconds(timeout.Value);
				}

				foreach (var row in rows.Rows)
				{
					await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);

					for (var ordinal = 0; ordinal < row.Length; ordinal++)
					{
						var value = PostgreSqlValues.ForColumn(row[ordinal], dataTypeNames[ordinal]);
						if (value == null)
						{
							await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);

							continue;
						}

						await importer.WriteAsync(value, dataTypeNames[ordinal], cancellationToken).ConfigureAwait(false);
					}
				}

				await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				// A connection opened here is closed here, as for every other command
				if (wasClosed)
				{
					await connection.CloseAsync().ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		/// Binary COPY converts nothing on the server, so every value is written as the type its column has.
		/// The types are read from the table itself, through a query that returns no row.
		/// </summary>
		private static async Task<string[]> ReadDataTypeNamesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName, string columnList, CancellationToken cancellationToken)
		{
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT {columnList} FROM {tableName} LIMIT 0";
			command.Transaction = transaction;

			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken).ConfigureAwait(false);

			return Enumerable.Range(0, reader.FieldCount).Select(reader.GetDataTypeName).ToArray();
		}
	}
}