using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Eshava.Storm.Extensions;
using Eshava.Storm.Handler;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class ParameterTests
	{
		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			new LabelTypeHandler().AddTypeHandler();
		}

		[TestMethod]
		public async Task ListDoesNotReplaceALongerParameterNameTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();

			// Act
			var count = await connection.ExecuteScalarAsync<long>(
				"SELECT COUNT(*) FROM Items WHERE Id IN @Id AND Name <> @IdName",
				new { Id = new[] { 1, 3 }, IdName = "none" }
			);

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task ListDoesNotReplaceAnExpandedLongerListTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();

			// Act: Ids is expanded first, the expansion of Id must not touch @Ids_p0
			var count = await connection.ExecuteScalarAsync<long>(
				"SELECT COUNT(*) FROM Items WHERE Id IN @Ids OR Id IN @Id",
				new { Ids = new[] { 1 }, Id = new[] { 3 } }
			);

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task ListInInterpolatedStatementWithTenOrMoreParametersTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();
			var ids = new[] { 1, 2 };
			var name = "none";

			// The list is the first parameter, so its name is a prefix of the tenth one
			SqlInterpolatedStringHandler query = $"SELECT COUNT(*) FROM Items WHERE Id IN @{ids} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name} AND Name <> @{name}";

			// Act
			var count = await connection.ExecuteScalarAsync<long>(ref query);

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EmptyListIsAnEmptySetTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();

			// Act
			var inCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Id IN @Ids", new { Ids = new int[0] });
			var notInCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Id NOT IN @Ids", new { Ids = new List<int>() });

			// Assert
			inCount.Should().Be(0);
			notInCount.Should().Be(3);
		}

		[TestMethod]
		public async Task ListNamedWithAtSignInDictionaryTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();
			var parameters = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("@Ids", new[] { 1, 3 }) };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Id IN @Ids", parameters);

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task DictionaryWithTypedValuesTest()
		{
			// Arrange
			using var connection = await CreateDatabaseAsync();
			var parameters = new Dictionary<string, int> { { "Id", 2 } };

			// Act
			var name = await connection.ExecuteScalarAsync<string>("SELECT Name FROM Items WHERE Id = @Id", parameters);

			// Assert
			name.Should().Be("two");
		}

		[TestMethod]
		public async Task HandlerParameterIsNotSqlVariantTest()
		{
			// Arrange
			var connection = new RecordingConnection();

			// Act
			await connection.ExecuteAsync("UPDATE Items SET Name = @Label", new { Label = new Label("text") });

			// Assert
			var parameter = connection.LastCommand.SqlParameters["Label"];
			parameter.Value.Should().Be("text");
			parameter.SqlDbType.Should().Be(SqlDbType.NVarChar);
		}

		private static Task<SqliteConnection> CreateDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO Items (Id, Name) VALUES (1, 'one'), (2, 'two'), (3, 'three')"
			);
		}

		private sealed class Label
		{
			public Label(string text)
			{
				Text = text;
			}

			public string Text { get; }
		}

		private class LabelTypeHandler : StringTypeHandler<Label>
		{
			protected override string Format(Label label)
			{
				return label.Text;
			}

			protected override Label Parse(string text)
			{
				return new Label(text);
			}
		}
	}
}