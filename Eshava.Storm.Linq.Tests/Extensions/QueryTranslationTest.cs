using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;
using Eshava.Storm.Linq.Tests.Models;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Linq.Tests.Extensions
{
	[TestClass, TestCategory("Extensions")]
	public class QueryTranslationTest
	{
		[TestMethod]
		public void ExistingWhereWithOrIsEnclosedTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a WHERE a.Beta = 0 OR a.Epsilon = 0";
			var conditions = Conditions(alpha => alpha.Gamma == "One");

			// Act
			var result = conditions.AddWhereConditionsToQuery(query, MappedTo("a"));

			// Assert
			result.Sql.Should().Be($"SELECT * FROM Alpha a WHERE (a.Beta = 0 OR a.Epsilon = 0){Environment.NewLine}AND{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void WordContainingWhereIsNoKeywordTest()
		{
			// Arrange
			var query = "SELECT a.SomewhereId FROM Alpha a";
			var conditions = Conditions(alpha => alpha.Gamma == "One");

			// Act
			var result = conditions.AddWhereConditionsToQuery(query, MappedTo("a"));

			// Assert
			result.Sql.Should().Be($"{query}{Environment.NewLine}WHERE{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void WhereOfASubqueryInsideTheWhereTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a WHERE a.Beta IN (SELECT o.Beta FROM Omega o WHERE o.IsActive = 1)";
			var conditions = Conditions(alpha => alpha.Gamma == "One");

			// Act
			var result = conditions.AddWhereConditionsToQuery(query, MappedTo("a"));

			// Assert
			result.Sql.Should().Be($"{query}{Environment.NewLine}AND{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void ConditionsAreInsertedBeforeOrderByTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a ORDER BY a.Beta";
			var conditions = Conditions(alpha => alpha.Gamma == "One");

			// Act
			var result = conditions.AddWhereConditionsToQuery(query, MappedTo("a"));

			// Assert
			result.Sql.Should().Be($"SELECT * FROM Alpha a{Environment.NewLine}WHERE{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}ORDER BY a.Beta");
		}

		[TestMethod]
		public void CapturedObjectOfAMappedTypeIsAValueTest()
		{
			// Arrange
			var request = new { Model = new Alpha { Gamma = "One" } };
			var settings = new WhereQuerySettings { PropertyTypeMappings = new Dictionary<Type, string> { { typeof(Alpha), "a" } } };
			var conditions = Conditions(alpha => alpha.Gamma == request.Model.Gamma);

			// Act
			var result = conditions.CalculateWhereConditions(settings);

			// Assert
			result.Sql.Should().Be($"(a.Gamma = @p0){Environment.NewLine}");
			result.QueryParameter["p0"].Should().Be("One");
		}

		[TestMethod]
		public void LikeWildcardsAreEscapedTest()
		{
			// Arrange
			var conditions = Conditions(alpha => alpha.Gamma.Contains("50%_[x]"));

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"Gamma LIKE @p0{Environment.NewLine}");
			result.QueryParameter["p0"].Should().Be("%50[%][_][[]x]%");
		}

		[TestMethod]
		public void NullThroughACapturedMemberIsATestForNullTest()
		{
			// Arrange
			var filter = new Omega { Psi = null };
			var conditions = Conditions(alpha => alpha.Gamma == filter.Psi, alpha => alpha.Gamma != filter.Psi);

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"(Gamma IS NULL){Environment.NewLine}AND{Environment.NewLine}(Gamma IS NOT NULL){Environment.NewLine}");
			result.QueryParameter.Should().BeEmpty();
		}

		[TestMethod]
		public void StaticMembersAndCallsWithoutColumnAreValuesTest()
		{
			// Arrange
			var conditions = Conditions(
				alpha => alpha.Id != Guid.Empty,
				alpha => alpha.Gamma == string.Empty,
				alpha => alpha.Zeta > DateTime.UtcNow.AddDays(-1)
			);

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"(Id != @p0){Environment.NewLine}AND{Environment.NewLine}(Gamma = @p1){Environment.NewLine}AND{Environment.NewLine}(Zeta > @p2){Environment.NewLine}");
			result.QueryParameter.Should().HaveCount(3);
			result.QueryParameter["p0"].Should().Be(Guid.Empty);
			result.QueryParameter["p1"].Should().Be("");
		}

		[TestMethod]
		public void UnsupportedPartsAreRefusedTest()
		{
			// Arrange
			var search = "One Two";
			var staticMethodOnColumn = Conditions(alpha => Math.Abs(alpha.Beta) > 100);
			var arithmeticOnColumn = Conditions(alpha => alpha.Beta + 1 > 5);
			var columnInsideAValue = Conditions(alpha => search.Contains(alpha.Gamma));
			var anyOverAColumn = Conditions(alpha => alpha.Omega.Psi.Any(character => character == 'x'));

			// Act
			var translations = new Action[]
			{
				() => staticMethodOnColumn.CalculateWhereConditions(),
				() => arithmeticOnColumn.CalculateWhereConditions(),
				() => columnInsideAValue.CalculateWhereConditions(),
				() => anyOverAColumn.CalculateWhereConditions()
			};

			// Assert
			foreach (var translation in translations)
			{
				translation.Should().Throw<NotSupportedException>();
			}
		}

		[TestMethod]
		public void ColumnsOnBothSidesAndValuesOnTheLeftTest()
		{
			// Arrange
			var conditions = Conditions(
				alpha => alpha.Beta < alpha.Epsilon,
				alpha => 5 < alpha.Beta,
				alpha => null == alpha.Gamma
			);

			// Act
			var result = conditions.CalculateWhereConditions(new WhereQuerySettings { PropertyMappings = new Dictionary<string, string> { { ".", "a" } } });

			// Assert
			result.Sql.Should().Be($"(a.Beta < a.Epsilon){Environment.NewLine}AND{Environment.NewLine}(@p0 < a.Beta){Environment.NewLine}AND{Environment.NewLine}(a.Gamma IS NULL){Environment.NewLine}");
		}

		[TestMethod]
		public void EmptyListsMatchNothingTest()
		{
			// Arrange
			var empty = new List<int>();
			var conditions = Conditions(
				alpha => empty.Contains(alpha.Beta),
				alpha => !empty.Contains(alpha.Beta),
				alpha => empty.Any(value => value == alpha.Beta)
			);

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"(1 = 0){Environment.NewLine}AND{Environment.NewLine}NOT(1 = 0){Environment.NewLine}AND{Environment.NewLine}(1 = 0){Environment.NewLine}");
			result.QueryParameter.Should().BeEmpty();
		}

		[TestMethod]
		public void SettingsAreNotChangedAndCanBeReusedTest()
		{
			// Arrange
			var settings = new WhereQuerySettings { QueryParameter = new Dictionary<string, object> { { "p0", 1 } } };
			var conditions = Conditions(alpha => alpha.Gamma == "One");

			// Act
			var first = conditions.CalculateWhereConditions(settings);
			var second = conditions.CalculateWhereConditions(settings);

			// Assert
			settings.QueryParameter.Should().HaveCount(1);
			first.Sql.Should().Be($"(Gamma = @p1){Environment.NewLine}");
			first.QueryParameter.Keys.Should().Equal("p0", "p1");
			second.QueryParameter.Keys.Should().Equal("p0", "p1");
		}

		[TestMethod]
		public void AnyWithMembersOfTheItemTest()
		{
			// Arrange
			var first = Guid.NewGuid();
			var second = Guid.NewGuid();
			var omegas = new List<Omega> { new Omega { Id = first }, new Omega { Id = second } };
			var names = new[] { "a", "b" };
			var conditions = Conditions(
				alpha => omegas.Any(omega => omega.Id == alpha.Id),
				alpha => names.Any(name => alpha.Gamma.StartsWith(name))
			);

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"((@p0 = Id) OR (@p1 = Id)){Environment.NewLine}AND{Environment.NewLine}(Gamma LIKE @p2 OR Gamma LIKE @p3){Environment.NewLine}");
			result.QueryParameter.Values.Should().Equal(first, second, "a%", "b%");
		}

		[TestMethod]
		public void SearchTermNullIsRefusedTest()
		{
			// Arrange
			string term = null;
			var conditions = Conditions(alpha => alpha.Gamma.StartsWith(term));

			// Act
			var translation = () => conditions.CalculateWhereConditions();

			// Assert
			translation.Should().Throw<ArgumentNullException>();
		}

		[TestMethod]
		public void EnumBasedOnLongKeepsItsRangeTest()
		{
			// Arrange
			var enumConstant = new List<Expression<Func<Alpha, bool>>> { BuildEnumComparison(Capacity.Large) };

			// Act
			var result = enumConstant.CalculateWhereConditions();

			// Assert
			result.QueryParameter["p0"].Should().Be(5000000000L);
		}

		[TestMethod]
		public void CapturedBooleanBecomesALiteralPredicateTest()
		{
			// Arrange
			var flag = true;
			var conditions = Conditions(alpha => flag && alpha.IsActive, alpha => !flag || alpha.IsActive);

			// Act
			var result = conditions.CalculateWhereConditions();

			// Assert
			result.Sql.Should().Be($"((1 = 1) AND (IsActive = @p0)){Environment.NewLine}AND{Environment.NewLine}((1 = 0) OR (IsActive = @p1)){Environment.NewLine}");
		}

		private static List<Expression<Func<Alpha, bool>>> Conditions(params Expression<Func<Alpha, bool>>[] conditions)
		{
			return conditions.ToList();
		}

		private static WhereQuerySettings MappedTo(string alias)
		{
			return new WhereQuerySettings { PropertyMappings = new Dictionary<string, string> { { ".", alias } } };
		}

		/// <summary>
		/// An enum constant compared with a column, as a filter engine builds it: the constant is typed as the enum itself
		/// </summary>
		private static Expression<Func<Alpha, bool>> BuildEnumComparison(Capacity capacity)
		{
			var parameter = Expression.Parameter(typeof(Alpha), "alpha");
			var member = Expression.Convert(Expression.Property(parameter, nameof(Alpha.Beta)), typeof(object));
			var constant = Expression.Convert(Expression.Constant(capacity), typeof(object));

			return Expression.Lambda<Func<Alpha, bool>>(Expression.Equal(member, constant), parameter);
		}

		private enum Capacity : long
		{
			Small = 1,
			Large = 5000000000
		}
	}
}