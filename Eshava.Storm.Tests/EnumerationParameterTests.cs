using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Eshava.Storm.Extensions;
using Eshava.Storm.Handler;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class EnumerationParameterTests
	{
		private static readonly byte[] _hashA = [0x01, 0x02, 0x03];
		private static readonly byte[] _hashB = [0x04, 0x05, 0x06];
		private static readonly byte[] _hashC = [0x07, 0x08, 0x09];

		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			new ByteArrayTypeHandler().AddTypeHandler();
			new DateAsTextTypeHandler().AddTypeHandler();
		}

		[TestMethod]
		public async Task EnumerationOfTypeWithHandlerTest()
		{
			// Arrange
			using var connection = await CreateConnectionAsync();
			await connection.ExecuteAsync("CREATE TABLE Items (Id INTEGER NOT NULL, Fingerprint BLOB NOT NULL)");
			await connection.ExecuteAsync("INSERT INTO Items (Id, Fingerprint) VALUES (1, @A), (2, @B), (3, @C)", new { A = _hashA, B = _hashB, C = _hashC });

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Fingerprint IN @Fingerprints", new { Fingerprints = new[] { _hashA, _hashC } });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfTypeWithoutHandlerTest()
		{
			// Arrange
			using var connection = await CreateConnectionAsync();
			await connection.ExecuteAsync("CREATE TABLE Items (Id INTEGER NOT NULL)");
			await connection.ExecuteAsync("INSERT INTO Items (Id) VALUES (1), (2), (3)");

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Id IN @Ids", new { Ids = new[] { 1, 3 } });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfDateTimeWithHandlerTest()
		{
			// Arrange
			using var connection = await CreateConnectionAsync();
			await CreateDateTableAsync(connection);

			// The times differ from the stored dates, so only a value set through the handler can match
			var days = new[] { new DateTime(2026, 9, 1, 8, 15, 0), new DateTime(2026, 9, 3, 17, 45, 0) };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Day IN @Days", new { Days = days });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfNullableDateTimeWithNullElementTest()
		{
			// Arrange
			using var connection = await CreateConnectionAsync();
			await CreateDateTableAsync(connection);

			var days = new DateTime?[] { new DateTime(2026, 9, 2, 12, 0, 0), null };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Day IN @Days", new { Days = days });

			// Assert
			count.Should().Be(1);
		}

		private static async Task<SqliteConnection> CreateConnectionAsync()
		{
			// An in-memory database lives as long as its connection, so it is opened here and kept open
			var connection = new SqliteConnection("Data Source=:memory:");
			await connection.OpenAsync();

			return connection;
		}

		private static async Task CreateDateTableAsync(SqliteConnection connection)
		{
			await connection.ExecuteAsync("CREATE TABLE Items (Id INTEGER NOT NULL, Day TEXT NOT NULL)");
			await connection.ExecuteAsync("INSERT INTO Items (Id, Day) VALUES (1, '2026-09-01'), (2, '2026-09-02'), (3, '2026-09-03')");
		}

		private class ByteArrayTypeHandler : TypeHandler<byte[]>
		{
			public override void SetValue(IDbDataParameter parameter, byte[] value)
			{
				parameter.DbType = DbType.Binary;
				parameter.Value = value;
			}

			public override byte[] Parse(object value)
			{
				return (byte[])value;
			}
		}

		private class DateAsTextTypeHandler : TypeHandler<DateTime>
		{
			public override void SetValue(IDbDataParameter parameter, DateTime value)
			{
				parameter.Value = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			}

			public override DateTime Parse(object value)
			{
				return DateTime.ParseExact((string)value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			}
		}
	}
}