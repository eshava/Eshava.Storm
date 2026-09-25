using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Attributes;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class MappingTests
	{
		[TestMethod]
		public async Task StatementWithoutFromIsMappedTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync();

			// Act
			var result = (await connection.QueryAsync<Item>("SELECT 7 AS Id, 'seven' AS Name")).Single();

			// Assert
			result.Id.Should().Be(7);
			result.Name.Should().Be("seven");
		}

		[TestMethod]
		public async Task ComputedColumnIsMappedWithTableAliasTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				"SELECT i.Id, i.Name, (SELECT COUNT(*) FROM Items) AS Total FROM Items i WHERE i.Id = 1",
				mapper => mapper.Map<ItemWithTotal>("i")
			);

			// Assert
			result.Single().Total.Should().Be(3);
		}

		[TestMethod]
		public async Task AliasIsNotFoundInsideALongerAliasTest()
		{
			// Arrange
			using var connection = await CreateEmployeeDatabaseAsync();

			// Act
			var result = (await connection.QueryAsync(
				"SELECT le.Name AS LeaderName, m.Id, m.Name, e.Id, e.Name FROM Employees e JOIN Employees m ON m.Id = e.ManagerId JOIN Employees le ON le.Id = m.ManagerId",
				mapper => (Employee: mapper.Map<Employee>("e"), Manager: mapper.Map<Employee>("m"))
			)).Single();

			// Assert
			result.Employee.Name.Should().Be("Low");
			result.Manager.Name.Should().Be("Mid");
		}

		[TestMethod]
		public async Task RenamedColumnsAreMappedPerAliasTest()
		{
			// Arrange
			using var connection = await CreateEmployeeDatabaseAsync();

			// Act: the select list names the manager first, the FROM clause the employee
			var result = (await connection.QueryAsync(
				"SELECT m.Id, m.Name AS Label, e.Id, e.Name AS Label FROM Employees e JOIN Employees m ON m.Id = e.ManagerId WHERE e.Id = 3",
				mapper => (Employee: mapper.Map<LabelledEmployee>("e"), Manager: mapper.Map<LabelledEmployee>("m"))
			)).Single();

			// Assert
			result.Employee.Label.Should().Be("Low");
			result.Manager.Label.Should().Be("Mid");
		}

		[TestMethod]
		public async Task NestedOwnedObjectsAreReadFromTheColumnsTheyAreWrittenToTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Residents (Id INTEGER NOT NULL, Home_Street TEXT NULL, Home_Location_Latitude REAL NULL)"
			);
			await connection.InsertAsync<Resident, int>(new Resident { Id = 1, Home = new Address { Street = "Main", Location = new Location { Latitude = 52.5 } } });
			await connection.InsertAsync<Resident, int>(new Resident { Id = 2, Home = new Address { Street = "Side", Location = new Location { Latitude = 48.1 } } });

			// Act
			var result = (await connection.QueryAsync<Resident>("SELECT * FROM Residents ORDER BY Id")).ToList();

			// Assert
			result[0].Home.Location.Latitude.Should().Be(52.5);
			result[1].Home.Location.Latitude.Should().Be(48.1);
			result[0].Home.Should().NotBeSameAs(result[1].Home);
		}

		[TestMethod]
		public async Task AliasOfACommonTableExpressionIsMappedTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				"WITH Selected AS (SELECT i.Id, i.Name FROM Items i WHERE i.Id > 1) SELECT s.Id, s.Name FROM Selected s ORDER BY s.Id",
				mapper => mapper.Map<Item>("s")
			);

			// Assert
			result.Select(item => item.Name).Should().Equal("two", "three");
		}

		[TestMethod]
		public async Task CommaJoinedTableIsMappedTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = (await connection.QueryAsync(
				"SELECT a.Id, a.Name, b.Id, b.Name FROM Items a, Items b WHERE a.Id = 1 AND b.Id = 2",
				mapper => (First: mapper.Map<Item>("a"), Second: mapper.Map<Item>("b"))
			)).Single();

			// Assert
			result.First.Name.Should().Be("one");
			result.Second.Name.Should().Be("two");
		}

		[TestMethod]
		public async Task DerivedTableInFromIsMappedTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				"SELECT x.Id, x.Name FROM (SELECT Id, Name FROM Items WHERE Id = 3) x",
				mapper => mapper.Map<Item>("x")
			);

			// Assert
			result.Single().Name.Should().Be("three");
		}

		[TestMethod]
		public async Task TableNameCanBeUsedAsAliasTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				"SELECT i.Id, i.Name FROM Items i WHERE i.Id = 2",
				mapper => mapper.Map<Item>("items")
			);

			// Assert
			result.Single().Name.Should().Be("two");
		}

		[TestMethod]
		public async Task RepeatedDerivedTableAliasInUnionTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var result = await connection.QueryAsync(
				"SELECT i.Id, i.Name FROM Items i JOIN (SELECT Id FROM Items WHERE Id = 1) s ON s.Id = i.Id "
				+ "UNION ALL SELECT i.Id, i.Name FROM Items i JOIN (SELECT Id FROM Items WHERE Id = 2) s ON s.Id = i.Id",
				mapper => mapper.Map<Item>("i")
			);

			// Assert
			result.Select(item => item.Id).Should().BeEquivalentTo(new[] { 1, 2 });
		}

		[TestMethod]
		[DoNotParallelize]
		public async Task IgnoredDuplicatedColumnsAreReadFromTheFirstOccurrenceTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();
			const string SQL = "SELECT i.Id, i.Name, j.Name FROM Items i JOIN Items j ON j.Id = i.Id + 1 WHERE i.Id = 1";

			try
			{
				// Act
				var lastWins = (await connection.QueryAsync<Item>(SQL)).Single();
				Settings.IgnoreDuplicatedColumns = true;
				var firstWins = (await connection.QueryAsync<Item>(SQL)).Single();

				// Assert
				lastWins.Name.Should().Be("two");
				firstWins.Name.Should().Be("one");
			}
			finally
			{
				Settings.IgnoreDuplicatedColumns = false;
			}
		}

		[TestMethod]
		public async Task ReaderWithoutSchemaReportsColumnTypesTest()
		{
			// Arrange
			var table = new DataTable();
			table.Columns.Add("Id", typeof(int));
			table.Columns.Add("Name", typeof(string));
			table.Rows.Add(1, "one");
			var connection = new RecordingConnection { Reader = () => new PlainDataReader(table.CreateDataReader()) };

			// Act
			var result = (await connection.QueryAsync(
				"SELECT Id, Name FROM Items",
				mapper => (Item: mapper.Map<Item>(), IdType: mapper.GetDataType("Id"))
			)).Single();

			// Assert
			result.Item.Name.Should().Be("one");
			result.IdType.Should().Be(typeof(int));
		}

		[TestMethod]
		public async Task FirstOrDefaultReadsTheFirstRowOnlyTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act: evaluating the second row fails, so it must not be read; without ORDER BY SQLite evaluates row by row
			var result = await connection.QueryFirstOrDefaultAsync<Item>(
				"SELECT Id, CASE WHEN Id = 1 THEN Name ELSE json('{') END AS Name FROM Items"
			);

			// Assert
			result.Name.Should().Be("one");
		}

		private static Task<SqliteConnection> CreateItemDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO Items (Id, Name) VALUES (1, 'one'), (2, 'two'), (3, 'three')"
			);
		}

		private static Task<SqliteConnection> CreateEmployeeDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Employees (Id INTEGER NOT NULL, Name TEXT NOT NULL, ManagerId INTEGER NULL)",
				"INSERT INTO Employees (Id, Name, ManagerId) VALUES (1, 'Boss', NULL), (2, 'Mid', 1), (3, 'Low', 2)"
			);
		}

		private class Item
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private class ItemWithTotal
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public long Total { get; set; }
		}

		private class Employee
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private class LabelledEmployee
		{
			public int Id { get; set; }
			public string Label { get; set; }
		}

		private class Resident
		{
			public int Id { get; set; }

			[OwnsOne]
			public Address Home { get; set; }
		}

		private class Address
		{
			public string Street { get; set; }

			[OwnsOne]
			public Location Location { get; set; }
		}

		private class Location
		{
			public double Latitude { get; set; }
		}
	}
}