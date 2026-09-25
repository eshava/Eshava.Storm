using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Eshava.Storm.Linq.Enums;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;
using Eshava.Storm.Linq.Tests.Models;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Linq.Tests.Extensions
{
	[TestClass, TestCategory("Extensions")]
	public class DialectTest
	{
		[TestCleanup]
		public void Cleanup()
		{
			LinqSettings.SetDefaults();
		}

		[TestMethod]
		public void SqlServerIgnoresCaseConversionAndEscapesWithBracketsTest()
		{
			// Act
			var result = Conditions(alpha => alpha.Gamma.ToLower() == "one", alpha => alpha.Gamma.ToLower().Contains("5%_")).CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"(Gamma = @p0){Environment.NewLine}AND{Environment.NewLine}Gamma LIKE @p1{Environment.NewLine}");
			result.QueryParameter["p1"].Should().Be("%5[%][_]%");
		}

		[TestMethod]
		public void PostgreSqlTranslatesCaseConversionTest()
		{
			// Arrange
			LinqSettings.Dialect = QueryDialect.PostgreSql;
			var values = new List<string> { "one", "two" };
			var settings = new WhereQuerySettings { PropertyMappings = new Dictionary<string, string> { { ".", "a" } } };

			// Act
			var result = Conditions(
				alpha => alpha.Gamma.ToLower() == "one",
				alpha => alpha.Gamma.ToUpper().StartsWith("O"),
				alpha => values.Contains(alpha.Gamma.ToLower()),
				alpha => alpha.Gamma.EndsWith("e")
			).CalculateWhereConditions(settings);

			// Assert: a search without ToLower stays case-sensitive, as it reads
			result.Sql.Should().Be(
				$"(lower(a.Gamma) = @p0){Environment.NewLine}AND{Environment.NewLine}"
				+ $"a.Gamma ILIKE @p1{Environment.NewLine}AND{Environment.NewLine}"
				+ $"lower(a.Gamma) IN @p2Array{Environment.NewLine}AND{Environment.NewLine}"
				+ $"a.Gamma LIKE @p3{Environment.NewLine}"
			);
		}

		[TestMethod]
		public void PostgreSqlEscapesWithBackslashTest()
		{
			// Arrange
			LinqSettings.Dialect = QueryDialect.PostgreSql;

			// Act
			var result = Conditions(alpha => alpha.Gamma.Contains("5%_\\")).CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"Gamma LIKE @p0{Environment.NewLine}");
			result.QueryParameter["p0"].Should().Be("%5\\%\\_\\\\%");
		}

		[TestMethod]
		public void SqliteEscapesWithBackslashAndEscapeClauseTest()
		{
			// Arrange
			LinqSettings.Dialect = QueryDialect.Sqlite;

			// Act
			var result = Conditions(alpha => alpha.Gamma.StartsWith("5%"), alpha => alpha.Gamma.ToLower() == "one").CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"Gamma LIKE @p0 ESCAPE '\\'{Environment.NewLine}AND{Environment.NewLine}(Gamma = @p1){Environment.NewLine}");
			result.QueryParameter["p0"].Should().Be("5\\%%");
		}

		[TestMethod]
		public void SettingOverridesTheDialectTest()
		{
			// Arrange
			LinqSettings.Dialect = QueryDialect.PostgreSql;
			LinqSettings.TranslateCaseConversion = false;
			var ignoredOnPostgreSql = Conditions(alpha => alpha.Gamma.ToLower() == "one", alpha => alpha.Gamma.ToLower().Contains("n")).CalculateWhereConditions();

			LinqSettings.Dialect = QueryDialect.SqlServer;
			LinqSettings.TranslateCaseConversion = true;
			var translatedOnSqlServer = Conditions(alpha => alpha.Gamma.ToLower() == "one", alpha => alpha.Gamma.ToLower().Contains("n")).CalculateWhereConditions();

			// Assert: ILIKE is PostgreSQL only, SQL Server lowers the column instead
			ignoredOnPostgreSql.Sql.Should().Be($"(Gamma = @p0){Environment.NewLine}AND{Environment.NewLine}Gamma LIKE @p1{Environment.NewLine}");
			translatedOnSqlServer.Sql.Should().Be($"(lower(Gamma) = @p0){Environment.NewLine}AND{Environment.NewLine}lower(Gamma) LIKE @p1{Environment.NewLine}");
		}

		private static List<Expression<Func<Alpha, bool>>> Conditions(params Expression<Func<Alpha, bool>>[] conditions)
		{
			return conditions.ToList();
		}
	}
}