using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Extensions;
using Eshava.Storm.Handler;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	/// <summary>
	/// Type handlers are registered process wide. The handler for DateTime registered here applies to every test
	/// in this assembly, so no other test may read or write DateTime values.
	/// </summary>
	[TestClass]
	public sealed class EnumerationParameterTests
	{
		private static readonly byte[] _hashA = [0x01, 0x02, 0x03];
		private static readonly byte[] _hashB = [0x04, 0x05, 0x06];
		private static readonly byte[] _hashC = [0x07, 0x08, 0x09];

		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			new FingerprintTypeHandler().AddTypeHandler();
			new DateAsTextTypeHandler().AddTypeHandler();
		}

		[TestMethod]
		public async Task EnumerationOfTypeWithHandlerTest()
		{
			// Arrange
			using var connection = await CreateFingerprintDatabaseAsync();
			var fingerprints = new[] { new Fingerprint(_hashA), new Fingerprint(_hashC) };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Fingerprint IN @Fingerprints", new { Fingerprints = fingerprints });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfByteArraysWithoutHandlerTest()
		{
			// Arrange
			using var connection = await CreateFingerprintDatabaseAsync();

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Fingerprint IN @Fingerprints", new { Fingerprints = new[] { _hashA, _hashC } });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task ByteArrayWithoutHandlerIsOneBinaryParameterTest()
		{
			// Arrange
			using var connection = await CreateFingerprintDatabaseAsync();

			// Act
			var id = await connection.ExecuteScalarAsync<long>("SELECT Id FROM Items WHERE Fingerprint = @Fingerprint", new { Fingerprint = _hashB });

			// Assert
			id.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfTypeWithoutHandlerTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL)",
				"INSERT INTO Items (Id) VALUES (1), (2), (3)"
			);

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Id IN @Ids", new { Ids = new[] { 1, 3 } });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfNullableEnumTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Colour INTEGER NULL)",
				"INSERT INTO Items (Id, Colour) VALUES (1, 1), (2, 2), (3, 3)"
			);
			var colours = new Colour?[] { Colour.Red, Colour.Blue };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Colour IN @Colours", new { Colours = colours });

			// Assert
			count.Should().Be(2);
		}

		[TestMethod]
		public async Task EnumerationOfDateTimeWithHandlerTest()
		{
			// Arrange
			using var connection = await CreateDateDatabaseAsync();

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
			using var connection = await CreateDateDatabaseAsync();

			var days = new DateTime?[] { new DateTime(2026, 9, 2, 12, 0, 0), null };

			// Act
			var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items WHERE Day IN @Days", new { Days = days });

			// Assert
			count.Should().Be(1);
		}

		private static Task<Microsoft.Data.Sqlite.SqliteConnection> CreateFingerprintDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Fingerprint BLOB NOT NULL)",
				"INSERT INTO Items (Id, Fingerprint) VALUES (1, X'010203'), (2, X'040506'), (3, X'070809')"
			);
		}

		private static Task<Microsoft.Data.Sqlite.SqliteConnection> CreateDateDatabaseAsync()
		{
			return SqliteDatabase.OpenAsync(
				"CREATE TABLE Items (Id INTEGER NOT NULL, Day TEXT NOT NULL)",
				"INSERT INTO Items (Id, Day) VALUES (1, '2026-09-01'), (2, '2026-09-02'), (3, '2026-09-03')"
			);
		}

		private enum Colour
		{
			Red = 1,
			Green = 2,
			Blue = 3
		}

		private sealed class Fingerprint
		{
			public Fingerprint(byte[] value)
			{
				Value = value;
			}

			public byte[] Value { get; }
		}

		private class FingerprintTypeHandler : TypeHandler<Fingerprint>
		{
			public override void SetValue(IDbDataParameter parameter, Fingerprint value)
			{
				parameter.DbType = DbType.Binary;
				parameter.Value = value.Value;
			}

			public override Fingerprint Parse(object value)
			{
				return new Fingerprint(((byte[])value).ToArray());
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