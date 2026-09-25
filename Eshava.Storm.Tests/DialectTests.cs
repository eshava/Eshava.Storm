using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Enums;
using Eshava.Storm.MetaData;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class DialectTests
	{
		[TestMethod]
		public async Task SqlServerStatementsAreUnchangedTest()
		{
			// Arrange
			var connection = new RecordingConnection();
			var nl = Environment.NewLine;

			// Act
			await connection.InsertAsync<Widget, int>(new Widget { Id = 1, Name = "widget" });
			var insert = connection.LastCommand.CommandText;
			await connection.InsertAsync<Gadget, int>(new Gadget { Name = "gadget" });
			var insertWithGeneratedKey = connection.LastCommand.CommandText;
			await connection.UpdateAsync(new Widget { Id = 1, Name = "widget" });
			var update = connection.LastCommand.CommandText;

			// Assert
			TypeAnalyzer.GetTableName<Widget>().Should().Be("[Widgets]");
			TypeAnalyzer.GetTableName<Order>().Should().Be("[Sales].[Orders]");
			insert.Should().Be($"INSERT INTO [Widgets]([Id],[Name]){nl}VALUES (@Id,@Name);{nl}SELECT @Id;{nl}");
			insertWithGeneratedKey.Should().Be($"INSERT INTO [Gadgets]([Name]){nl}VALUES (@Name);{nl}SELECT SCOPE_IDENTITY();{nl}");
			update.Should().Be($"UPDATE [Widgets]{nl} SET{nl}\t [Name] = @Name{nl}WHERE [Widgets].[Id] = @Id{nl}");
		}

		[TestMethod]
		[DoNotParallelize]
		public async Task PostgreSqlStatementsTest()
		{
			// Arrange
			var connection = new NpgsqlRecordingConnection();
			var nl = Environment.NewLine;
			Settings.Dialect = SqlDialect.PostgreSql;

			try
			{
				// Act
				await connection.InsertAsync<Gadget, int>(new Gadget { Name = "gadget" });
				var insertWithGeneratedKey = connection.LastCommand.CommandText;
				await connection.UpdateAsync(new Widget { Id = 1, Name = "widget" });
				var update = connection.LastCommand.CommandText;

				// Assert
				TypeAnalyzer.GetTableName<Widget>().Should().Be("\"widgets\"");
				TypeAnalyzer.GetTableName<Order>().Should().Be("\"sales\".\"orders\"");
				insertWithGeneratedKey.Should().Be($"INSERT INTO \"gadgets\"(\"name\"){nl}VALUES (@Name) RETURNING \"id\";{nl}");
				update.Should().Be($"UPDATE \"widgets\"{nl} SET{nl}\t \"name\" = @Name{nl}WHERE \"widgets\".\"id\" = @Id{nl}");
			}
			finally
			{
				Settings.Dialect = SqlDialect.SqlServer;
			}
		}

		[TestMethod]
		public async Task NamesQuotedWithDoubleQuotesAreAnalysedTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Widgets (Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO Widgets (Id, Name) VALUES (1, 'one'), (2, 'two')"
			);

			// Act
			var result = await connection.QueryAsync(
				"SELECT \"w\".\"Id\", \"w\".\"Name\" FROM \"Widgets\" \"w\" WHERE \"w\".\"Id\" = 2",
				mapper => mapper.Map<Widget>("w")
			);

			// Assert
			result.Single().Name.Should().Be("two");
		}

		private class Widget
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private class Gadget
		{
			[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
			public int Id { get; set; }

			public string Name { get; set; }
		}

		[Table("Orders", Schema = "Sales")]
		private class Order
		{
			public int Id { get; set; }
		}
	}
}