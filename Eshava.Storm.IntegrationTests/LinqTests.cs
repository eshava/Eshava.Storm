using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Eshava.Storm.IntegrationTests.Infrastructure;
using Eshava.Storm.Linq.Extensions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace Eshava.Storm.IntegrationTests
{
	/// <summary>
	/// Runs conditions translated by Eshava.Storm.Linq against PostgreSQL, which compares case-sensitively
	/// </summary>
	[TestClass]
	public sealed class LinqTests
	{
		[TestMethod]
		public async Task CaseConversionIsTranslatedTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();
			var names = new List<string> { "anna", "bob" };

			// Act
			var equal = await CountAsync(connection, person => person.Name.ToLower() == "anna");
			var contained = await CountAsync(connection, person => person.Name.ToLower().Contains("nn"));
			var inList = await CountAsync(connection, person => names.Contains(person.Name.ToLower()));
			var withoutConversion = await CountAsync(connection, person => person.Name.StartsWith("A"));

			// Assert
			equal.Should().Be(2);
			contained.Should().Be(2);
			inList.Should().Be(3);
			withoutConversion.Should().Be(1);
		}

		[TestMethod]
		public async Task WildcardsInASearchTermAreMatchedAsThemselvesTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var percentAndUnderscore = await CountAsync(connection, person => person.Name.Contains("%_"));
			var underscore = await CountAsync(connection, person => person.Name.Contains("_"));
			var backslash = await CountAsync(connection, person => person.Name.EndsWith("\\"));

			// Assert
			percentAndUnderscore.Should().Be(1);
			underscore.Should().Be(1);
			backslash.Should().Be(1);
		}

		private static async Task<long> CountAsync(NpgsqlConnection connection, Expression<Func<Person, bool>> condition)
		{
			var result = new List<Expression<Func<Person, bool>>> { condition }.AddWhereConditionsToQuery("SELECT count(*) FROM people");

			return await connection.ExecuteScalarAsync<long>(result.Sql, result.QueryParameter);
		}

		private static Task<NpgsqlConnection> CreateDatabaseAsync()
		{
			return PostgreSqlDatabase.OpenAsync(
				"DROP TABLE IF EXISTS people",
				"CREATE TABLE people (id int PRIMARY KEY, name text NOT NULL)",
				"INSERT INTO people (id, name) VALUES (1, 'Anna'), (2, 'anna'), (3, '50%_off'), (4, 'Bob'), (5, 'back\\')"
			);
		}

		private class Person
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}