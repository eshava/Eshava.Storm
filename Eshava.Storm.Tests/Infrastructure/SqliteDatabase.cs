using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Eshava.Storm.Tests.Infrastructure
{
	internal static class SqliteDatabase
	{
		/// <summary>
		/// An in-memory database lives as long as its connection, so it is opened here and kept open
		/// </summary>
		public static async Task<SqliteConnection> OpenAsync(params string[] statements)
		{
			var connection = new SqliteConnection("Data Source=:memory:");
			await connection.OpenAsync();

			foreach (var statement in statements)
			{
				using var command = connection.CreateCommand();
				command.CommandText = statement;
				await command.ExecuteNonQueryAsync();
			}

			return connection;
		}
	}
}