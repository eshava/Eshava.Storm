using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
	public sealed class CrudTests
	{
		[TestMethod]
		public async Task PartialUpdateWithIncompleteCompositeKeyIsRefusedTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE TenantItems (TenantId INTEGER NOT NULL, Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO TenantItems (TenantId, Id, Name) VALUES (1, 5, 'first'), (2, 5, 'second')"
			);

			// Act
			var partialUpdate = () => connection.UpdatePartialAsync<TenantItem>(new { Id = 5, Name = "changed" });
			var patchUpdate = () => connection.UpdatePatchAsync<TenantItem>(new[] { Pair("Id", 5), Pair("Name", "changed") });

			// Assert
			await partialUpdate.Should().ThrowAsync<ArgumentException>();
			await patchUpdate.Should().ThrowAsync<ArgumentException>();
			(await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM TenantItems WHERE Name = 'changed'")).Should().Be(0);
		}

		[TestMethod]
		public async Task UpdateWithoutColumnToSetIsRefusedTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var onlyKey = () => connection.UpdatePartialAsync<Item>(new { Id = 1 });
			var misspelt = () => connection.UpdatePartialAsync<Item>(new { Id = 1, Nmae = "changed" });

			// Assert
			await onlyKey.Should().ThrowAsync<ArgumentException>();
			await misspelt.Should().ThrowAsync<ArgumentException>();
		}

		[TestMethod]
		public async Task UpdateWithOwnedObjectRemovedClearsItsColumnsTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync("CREATE TABLE Residents (Id INTEGER NOT NULL, Home_Street TEXT NULL, Home_City TEXT NULL)");
			await connection.InsertAsync<Resident, int>(new Resident { Id = 1, Home = new Address { Street = "Main", City = "Springfield" } });

			// Act
			var updated = await connection.UpdateAsync(new Resident { Id = 1, Home = null });
			var result = await connection.QueryEntityAsync<Resident>(1);

			// Assert
			updated.Should().BeTrue();
			result.Home.Street.Should().BeNull();
			result.Home.City.Should().BeNull();
		}

		[TestMethod]
		public async Task PatchWithRenamedKeyColumnTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Products (product_id INTEGER NOT NULL, [Display Name] TEXT NOT NULL)",
				"INSERT INTO Products (product_id, [Display Name]) VALUES (1, 'old')"
			);

			// Act
			var updated = await connection.UpdatePatchAsync<Product>(new[] { Pair("Id", 1), Pair("Name", "new") });

			// Assert
			updated.Should().BeTrue();
			(await connection.ExecuteScalarAsync<string>("SELECT [Display Name] FROM Products WHERE product_id = 1")).Should().Be("new");
		}

		[TestMethod]
		public async Task KeyColumnNamedWithReservedWordTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync("CREATE TABLE Steps ([Order] INTEGER NOT NULL, Name TEXT NOT NULL)");

			// Act
			await connection.InsertAsync<Step, int>(new Step { Position = 1, Name = "first" });
			var updated = await connection.UpdateAsync(new Step { Position = 1, Name = "changed" });
			var result = await connection.QueryEntityAsync<Step>(1);
			var deleted = await connection.DeleteAsync(new Step { Position = 1 });

			// Assert
			updated.Should().BeTrue();
			result.Name.Should().Be("changed");
			deleted.Should().BeTrue();
		}

		[TestMethod]
		public async Task InsertReturnsAGuidKeyOnSqliteTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync("CREATE TABLE Documents (Id TEXT NOT NULL, Name TEXT NOT NULL)");
			var id = Guid.NewGuid();

			// Act
			var result = await connection.InsertAsync<Document, Guid>(new Document { Id = id, Name = "document" });

			// Assert
			result.Should().Be(id);
		}

		[TestMethod]
		public async Task WrappedSqliteConnectionUsesTheSqliteEngineTest()
		{
			// Arrange
			using var sqliteConnection = await SqliteDatabase.OpenAsync("CREATE TABLE Counters (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)");
			using var connection = new WrappingConnection(sqliteConnection);

			// Act: the key is generated, so the engine has to ask SQLite for it
			var first = await connection.InsertAsync<Counter, long>(new Counter { Name = "first" });
			var second = await connection.InsertAsync<Counter, long>(new Counter { Name = "second" });

			// Assert
			first.Should().Be(1);
			second.Should().Be(2);
		}

		[TestMethod]
		public async Task UpdateOfSeveralRowsThroughAListKeySucceedsTest()
		{
			// Arrange
			using var connection = await CreateItemDatabaseAsync();

			// Act
			var updated = await connection.UpdatePartialAsync<Item>(new { Id = new[] { 1, 2 }, Name = "changed" });

			// Assert
			updated.Should().BeTrue();
			(await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Name = 'changed'")).Should().Be(2);
		}

		private static KeyValuePair<string, object> Pair(string key, object value)
		{
			return new KeyValuePair<string, object>(key, value);
		}

		private static Task<SqliteConnection> CreateItemDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Name TEXT NOT NULL)",
				"INSERT INTO Items (Id, Name) VALUES (1, 'one'), (2, 'two'), (3, 'three')"
			);
		}

		private class Item
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}

		private class TenantItem
		{
			[Key]
			public int TenantId { get; set; }

			[Key]
			public int Id { get; set; }

			public string Name { get; set; }
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
			public string City { get; set; }
		}

		private class Product
		{
			[Key]
			[Column("product_id")]
			public int Id { get; set; }

			[Column("Display Name")]
			public string Name { get; set; }
		}

		private class Step
		{
			[Key]
			[Column("Order")]
			public int Position { get; set; }

			public string Name { get; set; }
		}

		private class Document
		{
			public Guid Id { get; set; }
			public string Name { get; set; }
		}

		private class Counter
		{
			[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
			public long Id { get; set; }

			public string Name { get; set; }
		}
	}
}