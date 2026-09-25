using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.IntegrationTests.Infrastructure;
using Eshava.Storm.MetaData;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.IntegrationTests
{
	[TestClass]
	public sealed class MappingTests
	{
		[TestMethod]
		public async Task AliasOfAQuotedTableNameTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act: the table name comes quoted in the dialect, "entries"
			var result = await connection.QueryAsync(
				$"SELECT e.id, e.name FROM {TypeAnalyzer.GetTableName<Entry>()} e WHERE e.id = 2",
				mapper => mapper.Map<Entry>("e")
			);

			// Assert
			result.Single().Name.Should().Be("two");
		}

		[TestMethod]
		public async Task JoinOfTwoAliasesInDefaultAndExplicitSchemaTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var result = (await connection.QueryAsync(
				$"""
				SELECT
					e.id, e.name, n.id, n.entryid, n.text
				FROM {TypeAnalyzer.GetTableName<Entry>()} e
				JOIN {TypeAnalyzer.GetTableName<Note>()} n ON n.entryid = e.id
				ORDER BY n.id
				""",
				mapper => (Entry: mapper.Map<Entry>("e"), Note: mapper.Map<Note>("n"))
			)).ToList();

			// Assert
			TypeAnalyzer.GetTableName<Note>().Should().Be("\"storm_notes\".\"notes\"");
			result.Should().HaveCount(2);
			result[0].Entry.Name.Should().Be("one");
			result[0].Note.Text.Should().Be("first note");
			result[1].Entry.Name.Should().Be("three");
			result[1].Note.Id.Should().Be(11);
		}

		[TestMethod]
		public async Task ComputedAndRenamedColumnsTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				$"SELECT e.id, e.name AS label, (SELECT count(*) FROM {TypeAnalyzer.GetTableName<Entry>()}) AS total FROM {TypeAnalyzer.GetTableName<Entry>()} e WHERE e.id = 1",
				mapper => mapper.Map<LabelledEntry>("e")
			);

			// Assert
			result.Single().Label.Should().Be("one");
			result.Single().Total.Should().Be(3);
		}

		[TestMethod]
		public async Task QueryFirstOrDefaultTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var result = await connection.QueryFirstOrDefaultAsync<Entry>($"SELECT * FROM {TypeAnalyzer.GetTableName<Entry>()} ORDER BY id DESC");

			// Assert
			result.Name.Should().Be("three");
		}

		private static Task<Npgsql.NpgsqlConnection> CreateDatabaseAsync()
		{
			return PostgreSqlDatabase.OpenAsync(
				"DROP TABLE IF EXISTS entries",
				"DROP SCHEMA IF EXISTS storm_notes CASCADE",
				"CREATE TABLE entries (id int PRIMARY KEY, name text NOT NULL)",
				"INSERT INTO entries (id, name) VALUES (1, 'one'), (2, 'two'), (3, 'three')",
				"CREATE SCHEMA storm_notes",
				"CREATE TABLE storm_notes.notes (id int PRIMARY KEY, entryid int NOT NULL, text text NOT NULL)",
				"INSERT INTO storm_notes.notes (id, entryid, text) VALUES (10, 1, 'first note'), (11, 3, 'second note')"
			);
		}

		private class Entry
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		[Table("Notes", Schema = "Storm_Notes")]
		private class Note
		{
			public int Id { get; set; }
			public int EntryId { get; set; }
			public string Text { get; set; }
		}

		private class LabelledEntry
		{
			public int Id { get; set; }
			public string Label { get; set; }
			public long Total { get; set; }
		}
	}
}