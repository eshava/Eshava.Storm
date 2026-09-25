using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Eshava.Storm.Linq;
using Eshava.Storm.Linq.Enums;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	/// <summary>
	/// Runs conditions translated by Eshava.Storm.Linq in the SQLite dialect, whose LIKE has no escape character of its own
	/// </summary>
	[TestClass]
	public sealed class LinqOnSqliteTests
	{
		[TestMethod]
		[DoNotParallelize]
		public async Task WildcardsInASearchTermAreMatchedAsThemselvesTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE People (Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO People (Id, Name) VALUES (1, 'Anna'), (2, '50%_off'), (3, 'Bob')"
			);
			LinqSettings.Dialect = QueryDialect.Sqlite;

			try
			{
				// Act
				var percentAndUnderscore = await CountAsync(connection, person => person.Name.Contains("%_"));
				var underscore = await CountAsync(connection, person => person.Name.Contains("_"));

				// Assert
				percentAndUnderscore.Should().Be(1);
				underscore.Should().Be(1);
			}
			finally
			{
				LinqSettings.SetDefaults();
			}
		}

		private static async Task<long> CountAsync(SqliteConnection connection, Expression<Func<Person, bool>> condition)
		{
			var result = new List<Expression<Func<Person, bool>>> { condition }.AddWhereConditionsToQuery("SELECT COUNT(*) FROM People");

			return await connection.ExecuteScalarAsync<long>(result.Sql, result.QueryParameter);
		}

		private class Person
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}