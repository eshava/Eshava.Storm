using System;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.MetaData;
using Eshava.Storm.MetaData.Builders;
using Eshava.Storm.MetaData.Interfaces;
using Eshava.Storm.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class MetadataTests
	{
		[TestMethod]
		public async Task ParallelFirstUseOfATypeSeesCompleteMetadataTest()
		{
			// Arrange: every closed generic type is a type no other test has analysed yet
			var inserts = new Func<Task<string>>[]
			{
				InsertRaceEntityAsync<byte>, InsertRaceEntityAsync<TimeSpan>, InsertRaceEntityAsync<short>, InsertRaceEntityAsync<int?>,
				InsertRaceEntityAsync<int>, InsertRaceEntityAsync<long?>, InsertRaceEntityAsync<long>, InsertRaceEntityAsync<DateTimeOffset>,
				InsertRaceEntityAsync<float>, InsertRaceEntityAsync<double>, InsertRaceEntityAsync<decimal>, InsertRaceEntityAsync<Guid>,
				InsertRaceEntityAsync<char>, InsertRaceEntityAsync<bool>
			};

			// Act
			var commandTexts = await Task.WhenAll(
				inserts.SelectMany(insert => Enumerable.Range(0, 32).Select(_ => Task.Run(insert)))
			);

			// Assert
			commandTexts.Should().OnlyContain(sql =>
				sql.Contains("[Id]") && sql.Contains("[Name]") && sql.Contains("[Description]") && sql.Contains("[Amount]") && sql.Contains("[Code]")
			);
		}

		[TestMethod]
		public async Task DateTimeOffsetAndDateOnlyPropertiesAreWrittenAndReadTest()
		{
			// Arrange
			using var connection = await SqliteDatabase.OpenAsync("CREATE TABLE Appointments (Id INTEGER NOT NULL, Scheduled TEXT NULL, Day TEXT NULL)");
			var appointment = new Appointment
			{
				Id = 1,
				Scheduled = new DateTimeOffset(2026, 9, 25, 10, 30, 0, TimeSpan.FromHours(2)),
				Day = new DateOnly(2026, 9, 25)
			};

			// Act
			await connection.InsertAsync<Appointment, int>(appointment);
			var result = (await connection.QueryAsync<Appointment>("SELECT Id, Scheduled, Day FROM Appointments")).Single();

			// Assert
			result.Scheduled.Should().Be(appointment.Scheduled);
			result.Day.Should().Be(appointment.Day);
		}

		[TestMethod]
		public async Task OwnsOneWithOneConfiguredPropertyKeepsTheOthersTest()
		{
			// Arrange
			TypeAnalyzer.AddType(new ResidentConfiguration());
			var connection = new RecordingConnection();

			// Act
			await connection.InsertAsync<Resident, int>(new Resident { Id = 1, Home = new Address { Street = "Main", City = "Springfield" } });

			// Assert
			connection.LastCommand.CommandText.Should().Contain("[Home_Road]").And.Contain("[Home_City]");
		}

		[TestMethod]
		[DoNotParallelize]
		public async Task RestrictedEngineRefusesUnregisteredTypeOnInsertTest()
		{
			// Arrange
			var connection = new RecordingConnection();
			Settings.RestrictToRegisteredModels = true;

			try
			{
				// Act
				var insert = () => connection.InsertAsync<Unregistered, int>(new Unregistered { Id = 1 });

				// Assert
				await insert.Should().ThrowAsync<ArgumentException>();
			}
			finally
			{
				Settings.RestrictToRegisteredModels = false;
			}
		}

		private static async Task<string> InsertRaceEntityAsync<T>()
		{
			var connection = new RecordingConnection();
			await connection.InsertAsync<RaceEntity<T>, int>(new RaceEntity<T> { Id = 1, Name = "Name", Description = "Description", Amount = 2, Code = default });

			return connection.LastCommand.CommandText;
		}

		private class RaceEntity<T>
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string Description { get; set; }
			public decimal Amount { get; set; }
			public T Code { get; set; }
		}

		private class Appointment
		{
			public int Id { get; set; }
			public DateTimeOffset? Scheduled { get; set; }
			public DateOnly? Day { get; set; }
		}

		private class Resident
		{
			public int Id { get; set; }
			public Address Home { get; set; }
		}

		private class Address
		{
			public string Street { get; set; }
			public string City { get; set; }
		}

		private class Unregistered
		{
			public int Id { get; set; }
		}

		private class ResidentConfiguration : IEntityTypeConfiguration<Resident>
		{
			public void Configure(EntityTypeBuilder<Resident> builder)
			{
				builder.ToTable("Residents");
				builder.OwnsOne(resident => resident.Home, home => home.Property(address => address.Street).HasColumnName("Road"));
			}
		}
	}
}