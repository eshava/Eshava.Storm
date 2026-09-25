using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Eshava.Storm.Attributes;
using Eshava.Storm.Models;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Tests
{
	[TestClass]
	public sealed class BulkInsertRowsTests
	{
		[TestMethod]
		public void ColumnsAndRowsTest()
		{
			// Arrange
			var entities = new[]
			{
				new Consignment { Name = "first", Status = ConsignmentStatus.Open, Destination = null },
				new Consignment { Name = "second", Status = ConsignmentStatus.Closed, Destination = new Destination { City = "Springfield" } }
			};

			// Act
			var bulkInsertRows = BulkInsertRows.Create(entities, () => new SqlParameter());
			var rows = bulkInsertRows.Rows.ToList();

			// Assert: the generated key is left out, the owned object contributes its column also where it is not set
			bulkInsertRows.TableName.Should().Be("[Consignments]");
			bulkInsertRows.Columns.Select(column => column.Name).Should().Equal("Name", "Status", "Destination_City");
			bulkInsertRows.Columns.Select(column => column.QuotedName).Should().Equal("[Name]", "[Status]", "[Destination_City]");
			rows[0].Should().Equal("first", 1, null);
			rows[1].Should().Equal("second", 2, "Springfield");
		}

		[TestMethod]
		public void CustomTableNameIsTakenAsItIsTest()
		{
			// Act
			var bulkInsertRows = BulkInsertRows.Create(new[] { new Consignment { Name = "first" } }, () => new SqlParameter(), "archive.consignments");

			// Assert
			bulkInsertRows.TableName.Should().Be("archive.consignments");
		}

		private enum ConsignmentStatus
		{
			Open = 1,
			Closed = 2
		}

		private class Consignment
		{
			[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
			public int Id { get; set; }

			public string Name { get; set; }
			public ConsignmentStatus Status { get; set; }

			[OwnsOne]
			public Destination Destination { get; set; }
		}

		private class Destination
		{
			public string City { get; set; }
		}
	}
}