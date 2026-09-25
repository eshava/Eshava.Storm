using System;
using System.Threading.Tasks;
using Eshava.Storm.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace Eshava.Storm.IntegrationTests.Infrastructure
{
	/// <summary>
	/// The database the tests run against, named by the environment variable STORM_POSTGRESQL_CONNECTION_STRING.
	/// The tests create and drop their own tables in it, so it should be a database of its own.
	/// Without the variable every test is reported as inconclusive rather than failed.
	/// </summary>
	[TestClass]
	public static class PostgreSqlDatabase
	{
		private const string CONNECTION_STRING_VARIABLE = "STORM_POSTGRESQL_CONNECTION_STRING";

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext context)
		{
			Settings.Dialect = SqlDialect.PostgreSql;
			Linq.LinqSettings.Dialect = Linq.Enums.QueryDialect.PostgreSql;
		}

		public static async Task<NpgsqlConnection> OpenAsync(params string[] statements)
		{
			var connectionString = Environment.GetEnvironmentVariable(CONNECTION_STRING_VARIABLE);
			if (String.IsNullOrEmpty(connectionString))
			{
				Assert.Inconclusive($"{CONNECTION_STRING_VARIABLE} is not set.");
			}

			var connection = new NpgsqlConnection(connectionString);
			await connection.OpenAsync();

			foreach (var statement in statements)
			{
				await using var command = connection.CreateCommand();
				command.CommandText = statement;
				await command.ExecuteNonQueryAsync();
			}

			return connection;
		}
	}
}