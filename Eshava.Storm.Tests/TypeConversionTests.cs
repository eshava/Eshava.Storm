using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Extensions;
using Eshava.Storm.Handler;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class TypeConversionTests
	{
		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			new ShadeAsTextTypeHandler().AddTypeHandler();
			new HashTypeHandler().AddTypeHandler();
		}

		[TestMethod]
		public async Task GuidStoredAsTextIsReadTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync();

			// Act
			var result = await connection.ExecuteScalarAsync<Guid>("SELECT '6f1c2d3e-4b5a-6978-8a9b-0c1d2e3f4a5b'");

			// Assert
			result.Should().Be(Guid.Parse("6f1c2d3e-4b5a-6978-8a9b-0c1d2e3f4a5b"));
		}

		[TestMethod]
		public async Task TimeSpanStoredAsTextIsReadTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync();

			// Act
			var result = await connection.ExecuteScalarAsync<TimeSpan>("SELECT '01:02:03'");

			// Assert
			result.Should().Be(new TimeSpan(1, 2, 3));
		}

		[TestMethod]
		public async Task EnumWithHandlerIsReadThroughTheHandlerTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync();

			// Act
			var result = await connection.ExecuteScalarAsync<Shade>("SELECT 'Dark'");

			// Assert
			result.Should().Be(Shade.Dark);
		}

		[TestMethod]
		public async Task EnumBasedOnLongKeepsItsRangeTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync();

			// Act
			var result = await connection.ExecuteScalarAsync<Capacity>("SELECT 5000000000");

			// Assert
			result.Should().Be(Capacity.Large);
		}

		[TestMethod]
		public async Task HandlerReadingBytesParsesOnceTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync(
				"CREATE TABLE Documents (Id INTEGER NOT NULL, Hash BLOB NOT NULL)",
				"INSERT INTO Documents (Id, Hash) VALUES (1, X'0A0B0C')"
			);

			// Act
			var result = (await connection.QueryAsync<Document>("SELECT Id, Hash FROM Documents")).Single();

			// Assert
			result.Hash.Value.Should().Equal(0x0A, 0x0B, 0x0C);
		}

		[TestMethod]
		public async Task DerivedDataTableUsesTheBuiltInHandlerTest()
		{
			// Arrange
			var connection = new RecordingConnection();
			var rows = new RowTable();
			rows.SetTypeName("dbo.RowType");

			// Act
			await connection.ExecuteAsync("EXEC dbo.Import @Rows", new { Rows = rows });

			// Assert
			var parameter = connection.LastCommand.SqlParameters["Rows"];
			parameter.SqlDbType.Should().Be(SqlDbType.Structured);
			parameter.TypeName.Should().Be("dbo.RowType");
		}

		[TestMethod]
		public async Task ListOfDataRecordsUsesTheBuiltInHandlerTest()
		{
			// Arrange
			var connection = new RecordingConnection();
			var metaData = new SqlMetaData("Id", SqlDbType.Int);
			var record = new SqlDataRecord(metaData);
			record.SetInt32(0, 1);

			// Act
			await connection.ExecuteAsync("EXEC dbo.Import @Rows", new { Rows = new List<SqlDataRecord> { record } });

			// Assert
			connection.LastCommand.SqlParameters.Count.Should().Be(1);
			connection.LastCommand.SqlParameters["Rows"].SqlDbType.Should().Be(SqlDbType.Structured);
		}

		private enum Shade
		{
			Light = 1,
			Dark = 2
		}

		private enum Capacity : long
		{
			Small = 1,
			Large = 5000000000
		}

		private class Document
		{
			public int Id { get; set; }
			public Hash Hash { get; set; }
		}

		private sealed class Hash
		{
			public Hash(byte[] value)
			{
				Value = value;
			}

			public byte[] Value { get; }
		}

		private class RowTable : DataTable
		{
		}

		private class ShadeAsTextTypeHandler : TypeHandler<Shade>
		{
			public override void SetValue(IDbDataParameter parameter, Shade value)
			{
				parameter.Value = value.ToString();
			}

			public override Shade Parse(object value)
			{
				return Enum.Parse<Shade>((string)value);
			}
		}

		private class HashTypeHandler : TypeHandler<Hash>
		{
			public override bool ReadAsByteArray => true;

			public override void SetValue(IDbDataParameter parameter, Hash value)
			{
				parameter.Value = value.Value;
			}

			public override Hash Parse(object value)
			{
				return new Hash((byte[])value);
			}
		}
	}
}