using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eshava.Storm.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.IntegrationTests
{
	[TestClass]
	public sealed class ParameterTests
	{
		private static readonly Guid _firstKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

		[TestMethod]
		public async Task ListsAndEmptyListsOfDifferentTypesTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var inInts = await Count(connection, "id IN @Values", new[] { 1, 3 });
			var inTexts = await Count(connection, "name IN @Values", new List<string> { "one", "two" });
			var emptyInts = await Count(connection, "id IN @Values", new int[0]);
			var notInEmptyInts = await Count(connection, "id NOT IN @Values", new int[0]);
			var emptyTexts = await Count(connection, "name IN @Values", new List<string>());
			var notInEmptyKeys = await Count(connection, "key NOT IN @Values", new List<Guid>());

			// Assert
			inInts.Should().Be(2);
			inTexts.Should().Be(2);
			emptyInts.Should().Be(0);
			notInEmptyInts.Should().Be(3);
			emptyTexts.Should().Be(0);
			notInEmptyKeys.Should().Be(3);
		}

		[TestMethod]
		public async Task ScalarParametersOfDifferentTypesTest()
		{
			// Arrange
			await using var connection = await CreateDatabaseAsync();

			// Act
			var byKey = await connection.ExecuteScalarAsync<string>("SELECT name FROM values WHERE key = @Key", new { Key = _firstKey });
			var byBytes = await connection.ExecuteScalarAsync<string>("SELECT name FROM values WHERE payload = @Payload", new { Payload = new byte[] { 0x02 } });
			var byUtcTime = await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM values WHERE created < @Time", new { Time = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc) });
			var byOffset = await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM values WHERE created < @Time", new { Time = new DateTimeOffset(2026, 9, 2, 2, 0, 0, TimeSpan.FromHours(2)) });

			// Assert
			byKey.Should().Be("one");
			byBytes.Should().Be("two");
			byUtcTime.Should().Be(1);
			byOffset.Should().Be(1);
		}

		private static Task<long> Count(Npgsql.NpgsqlConnection connection, string condition, object values)
		{
			return connection.ExecuteScalarAsync<long>($"SELECT count(*) FROM values WHERE {condition}", new { Values = values });
		}

		private static Task<Npgsql.NpgsqlConnection> CreateDatabaseAsync()
		{
			return PostgreSqlDatabase.OpenAsync(
				"DROP TABLE IF EXISTS values",
				"CREATE TABLE values (id int PRIMARY KEY, name text NOT NULL, key uuid NOT NULL, payload bytea NOT NULL, created timestamptz NOT NULL)",
				$"""
				INSERT INTO values (id, name, key, payload, created) VALUES
					(1, 'one', '{_firstKey}', '\x01', '2026-09-01 00:00:00+00'),
					(2, 'two', gen_random_uuid(), '\x02', '2026-09-02 00:00:00+00'),
					(3, 'three', gen_random_uuid(), '\x03', '2026-09-03 00:00:00+00')
				"""
			);
		}
	}
}