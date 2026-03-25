using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Eshava.Core.Linq;
using Eshava.Core.Linq.Enums;
using Eshava.Core.Linq.Models;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;
using Eshava.Storm.Linq.Tests.Enums;
using Eshava.Storm.Linq.Tests.Models;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Eshava.Storm.Linq.Tests.Extensions
{
	[TestClass, TestCategory("Extensions")]
	public class IEnumerableExtensionsTest
	{
		[TestInitialize]
		public void Setup()
		{

		}

		[TestMethod]
		public void CalculateWhereConditionsBooleanPropertyTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.IsActive && !alpha.IsActive,
				alpha => alpha.IsActive,
				alpha => !alpha.IsActive,
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(4);
			result.QueryParameter.ContainsKey("p0").Should().BeTrue();
			result.QueryParameter["p0"].Should().Be(true);
			result.QueryParameter.ContainsKey("p1").Should().BeTrue();
			result.QueryParameter["p1"].Should().Be(true);
			result.QueryParameter.ContainsKey("p2").Should().BeTrue();
			result.QueryParameter["p2"].Should().Be(true);
			result.QueryParameter.ContainsKey("p3").Should().BeTrue();
			result.QueryParameter["p3"].Should().Be(true);


			var sqlParts = result.Sql.Split(Environment.NewLine);
			sqlParts[0].Should().Be("((IsActive = @p0) AND NOT(IsActive = @p1))");
			sqlParts[1].Should().Be("AND");
			sqlParts[2].Should().Be("(IsActive = @p2)");
			sqlParts[3].Should().Be("AND");
			sqlParts[4].Should().Be("NOT(IsActive = @p3)");
		}

		[TestMethod]
		public void CalculateWhereConditionsSubObjectBooleanPropertyTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" },
					{ typeof(Omega), "o" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.IsActive && alpha.Omega.IsActive
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(true);
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.Last().Should().Be(true);

			result.Sql.Should().Be($"(NOT(a.IsActive = @p0) AND (o.IsActive = @p1)){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateWhereConditionsEqualTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsEqualObjectVauleTest()
		{
			// Arrange
			var filterAlpha = new Alpha { Gamma = "One" };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == filterAlpha.Gamma
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsEqualSubObjectVauleTest()
		{
			// Arrange
			var filterAlpha = new Alpha { Omega = new Omega { Psi = "One" } };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == filterAlpha.Omega.Psi
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsEqualSubObjectNullableVauleTest()
		{
			// Arrange
			var filterAlpha = new Alpha { Omega = new Omega { Id = Guid.Parse("9106c6e8-ccbe-4eb6-8d95-5073ef6d1aa0") } };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Id == filterAlpha.Omega.Id.Value
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(Guid.Parse("9106c6e8-ccbe-4eb6-8d95-5073ef6d1aa0"));

			result.Sql.Should().Be("(Id = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsEqualNullableVauleTest()
		{
			// Arrange
			Guid? value = Guid.Parse("9106c6e8-ccbe-4eb6-8d95-5073ef6d1aa0");
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Id == value
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(Guid.Parse("9106c6e8-ccbe-4eb6-8d95-5073ef6d1aa0"));

			result.Sql.Should().Be("(Id = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsNotEqualTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma != "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(Gamma != @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsIsNullTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == null
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(0);

			result.Sql.Should().Be("(Gamma IS NULL)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsIsNotNullTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma != null
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(0);

			result.Sql.Should().Be("(Gamma IS NOT NULL)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsGreaterThanTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Beta > 7
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(7);

			result.Sql.Should().Be("(Beta > @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsGreaterOrEqualTest()
		{
			// Arrange
			var betaId = 7;

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Beta >= betaId
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(7);

			result.Sql.Should().Be("(Beta >= @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsLessThanTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Beta < 7
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(7);

			result.Sql.Should().Be("(Beta < @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsLessOrEqualTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Beta <= 7
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be(7);

			result.Sql.Should().Be("(Beta <= @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsLikeTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma.Contains("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("%One%");

			result.Sql.Should().Be("Gamma LIKE @p0" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsNotLikeTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.Gamma.Contains("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("%One%");

			result.Sql.Should().Be("NOT(Gamma LIKE @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsStartsWithTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma.StartsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One%");

			result.Sql.Should().Be("Gamma LIKE @p0" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsNotStartsWithTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.Gamma.StartsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One%");

			result.Sql.Should().Be("NOT(Gamma LIKE @p0)" + Environment.NewLine);
		}

			[TestMethod]
		public void CalculateWhereConditionsNotStartsWithAndToUpperTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.Gamma.ToUpper().StartsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One%");

			result.Sql.Should().Be("NOT(Gamma LIKE @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsEndsWithTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma.EndsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("%One");

			result.Sql.Should().Be("Gamma LIKE @p0" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsNotEndsWithTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.Gamma.EndsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("%One");

			result.Sql.Should().Be("NOT(Gamma LIKE @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsNotEndsWithAndToLowerTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => !alpha.Gamma.ToLower().EndsWith("One")
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("%One");

			result.Sql.Should().Be("NOT(Gamma LIKE @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsContainedInArrayTest()
		{
			// Arrange
			var values = new[] { Color.Black, Color.White };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Contains(alpha.Delta)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0Array");
			result.QueryParameter.Values.First().Should().BeOfType(typeof(Color[]));

			result.Sql.Should().Be("Delta IN @p0Array" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsContainedInListTest()
		{
			// Arrange
			var values = new List<Color> { Color.Black, Color.White };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Contains(alpha.Delta)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0Array");
			result.QueryParameter.Values.First().Should().BeOfType(typeof(List<Color>));

			result.Sql.Should().Be("Delta IN @p0Array" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsContainedInArrayStringToLowerTest()
		{
			// Arrange
			var values = new[] { "alpha", "beta" };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Contains(alpha.Gamma.ToLower())
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0Array");
			result.QueryParameter.Values.First().Should().BeOfType(typeof(string[]));

			result.Sql.Should().Be("Gamma IN @p0Array" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsContainedInListStringToLowerTest()
		{
			// Arrange
			var values = new List<string> { "alpha", "beta" };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Contains(alpha.Gamma.ToLower())
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0Array");
			result.QueryParameter.Values.First().Should().BeOfType(typeof(List<string>));

			result.Sql.Should().Be("Gamma IN @p0Array" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsAnyEqualTest()
		{
			// Arrange
			var values = new List<Color> { Color.Black, Color.White };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Any(v => v == alpha.Delta)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be(Color.Black);
			result.QueryParameter.Values.Last().Should().Be(Color.White);

			result.Sql.Should().Be("((@p0 = Delta) OR (@p1 = Delta))" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsDateTimeTest()
		{
			// Arrange
			var values = new List<Color> { Color.Black, Color.White };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Zeta >= DateTime.UtcNow,
				alpha => alpha.Zeta <= DateTime.Now,
				alpha => alpha.Zeta == DateTime.Today
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(3);
			result.QueryParameter.ContainsKey("p0").Should().BeTrue();
			DateTime.UtcNow.Subtract((DateTime)result.QueryParameter["p0"]).Seconds.Should().BeInRange(0, 2);
			result.QueryParameter.ContainsKey("p1").Should().BeTrue();
			DateTime.Now.Subtract((DateTime)result.QueryParameter["p1"]).Seconds.Should().BeInRange(0, 2);
			result.QueryParameter.ContainsKey("p2").Should().BeTrue();
			result.QueryParameter["p2"].Should().Be(DateTime.Today);

			var sqlParts = result.Sql.Split(Environment.NewLine);
			sqlParts[0].Should().Be("(Zeta >= @p0)");
			sqlParts[1].Should().Be("AND");
			sqlParts[2].Should().Be("(Zeta <= @p1)");
			sqlParts[3].Should().Be("AND");
			sqlParts[4].Should().Be("(Zeta = @p2)");
		}

		[TestMethod]
		public void CalculateWhereConditionsAnyOrTest()
		{
			// Arrange
			var values = new List<int> { 10, 20 };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Any(v => alpha.Beta > v || v > alpha.Epsilon)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be(10);
			result.QueryParameter.Values.Last().Should().Be(20);

			result.Sql.Should().Be("(((Beta > @p0) OR (@p0 > Epsilon)) OR ((Beta > @p1) OR (@p1 > Epsilon)))" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsAnyAndTest()
		{
			// Arrange
			var values = new List<int> { 10, 20 };
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Any(v => alpha.Beta > v && v > alpha.Epsilon)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be(10);
			result.QueryParameter.Values.Last().Should().Be(20);

			result.Sql.Should().Be("(((Beta > @p0) AND (@p0 > Epsilon)) OR ((Beta > @p1) AND (@p1 > Epsilon)))" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsAnyAndWithPropertyTypeMappingTest()
		{
			// Arrange
			var values = new List<int> { 10, 20 };
			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => values.Any(v => alpha.Beta > v && v > alpha.Epsilon)
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be(10);
			result.QueryParameter.Values.Last().Should().Be(20);

			result.Sql.Should().Be("(((a.Beta > @p0) AND (@p0 > a.Epsilon)) OR ((a.Beta > @p1) AND (@p1 > a.Epsilon)))" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsMultipleConditionsTest()
		{
			// Arrange
			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One",
				alpha => alpha.Beta > 1
			};

			// Act
			var result = queryConditions.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be("One");
			result.QueryParameter.Values.Last().Should().Be(1);

			result.Sql.Should().Be($"(Gamma = @p0){Environment.NewLine}AND{Environment.NewLine}(Beta > @p1){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateWhereConditionsLinqWhereQueryEngineEnumGreaterThanTest()
		{
			// Arrange
			var whereQueryEngine = new WhereQueryEngine(new WhereQueryEngineOptions());
			var queryParameters = new QueryParameters
			{
				WhereQueryProperties = new List<WhereQueryProperty>
				{
					new WhereQueryProperty
					{
						Operator = CompareOperator.GreaterThan,
						PropertyName = nameof(Alpha.Delta),
						SearchTerm =  Color.White.ToString()
					},
				}
			};

			var whereConditions = whereQueryEngine.BuildQueryExpressions<Alpha>(queryParameters);


			// Act
			var result = whereConditions.Data.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be((int)Color.White);
			result.QueryParameter.Values.Last().Should().Be(0);

			result.Sql.Should().Be($"((CASE WHEN {nameof(Alpha.Delta)} = @p0 THEN 0 WHEN {nameof(Alpha.Delta)} > @p0 THEN 1 ELSE -1 END) > @p1){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateWhereConditionsLinqWhereQueryEngineEnumLessThanOrEqualTest()
		{
			// Arrange
			var whereQueryEngine = new WhereQueryEngine(new WhereQueryEngineOptions());
			var queryParameters = new QueryParameters
			{
				WhereQueryProperties = new List<WhereQueryProperty>
				{
					new WhereQueryProperty
					{
						Operator = CompareOperator.LessThanOrEqual,
						PropertyName = nameof(Alpha.Delta),
						SearchTerm =  Color.White.ToString()
					},
				}
			};

			var whereConditions = whereQueryEngine.BuildQueryExpressions<Alpha>(queryParameters);


			// Act
			var result = whereConditions.Data.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Keys.Last().Should().Be("p1");
			result.QueryParameter.Values.First().Should().Be((int)Color.White);
			result.QueryParameter.Values.Last().Should().Be(0);

			result.Sql.Should().Be($"((CASE WHEN {nameof(Alpha.Delta)} = @p0 THEN 0 WHEN {nameof(Alpha.Delta)} > @p0 THEN 1 ELSE -1 END) <= @p1){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateWhereConditionsLinqWhereQueryEngineContainedTest()
		{
			// Arrange
			var whereQueryEngine = new WhereQueryEngine(new WhereQueryEngineOptions());
			var queryParameters = new QueryParameters
			{
				WhereQueryProperties = new List<WhereQueryProperty>
				{
					new WhereQueryProperty
					{
						Operator = CompareOperator.ContainedIn,
						PropertyName = nameof(Alpha.Gamma),
						SearchTerm =  "Black|White"
					},
				}
			};

			var whereConditions = whereQueryEngine.BuildQueryExpressions<Alpha>(queryParameters);


			// Act
			var result = whereConditions.Data.CalculateWhereConditions();

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0Array");
			result.QueryParameter.Values.First().Should().BeOfType(typeof(List<string>));
			(result.QueryParameter.Values.First() as List<string>)[0].Should().Be("Black");
			(result.QueryParameter.Values.First() as List<string>)[1].Should().Be("White");

			result.Sql.Should().Be($"((Gamma IS NOT NULL) AND Gamma IN @p0Array){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateWhereConditionsPropertyMappingTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(a.Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsPropertyMappingSubClassTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Omega.Psi == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(o.Psi = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsPropertyAndPropertyTypeMappingSubClassTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ "a.Omega.Psi", "o.Psi" }
				},
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Omega.Psi == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(o.Psi = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsPropertyTypeMappingTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(a.Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsPropertyTypeMappingSubClassTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" },
					{ typeof(Omega), "o" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Omega.Psi == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be("(o.Psi = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void CalculateWhereConditionsQueryParameterTest()
		{
			// Arrange
			var data = new WhereQuerySettings
			{
				QueryParameter = new Dictionary<string, object>
				{
					{ "Beta", 6 }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.CalculateWhereConditions(data);

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("Beta");
			result.QueryParameter.Values.First().Should().Be(6);
			result.QueryParameter.Keys.Last().Should().Be("p0");
			result.QueryParameter.Values.Last().Should().Be("One");

			result.Sql.Should().Be("(Gamma = @p0)" + Environment.NewLine);
		}

		[TestMethod]
		public void AddWhereConditionsToQueryNoWhereTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a";
			var data = new WhereQuerySettings
			{
				QueryParameter = new Dictionary<string, object>
				{
					{ "Beta", 6 }
				},
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.AddWhereConditionsToQuery(query, data);

			// Assert
			result.QueryParameter.Should().HaveCount(2);
			result.QueryParameter.Keys.First().Should().Be("Beta");
			result.QueryParameter.Values.First().Should().Be(6);
			result.QueryParameter.Keys.Last().Should().Be("p0");
			result.QueryParameter.Values.Last().Should().Be("One");

			result.Sql.Should().Be($"{query}{Environment.NewLine}WHERE{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void AddWhereConditionsToQueryWithWhereTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a wHeRe a.Beta > 7";
			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.AddWhereConditionsToQuery(query, data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be($"{query}{Environment.NewLine}AND{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void AddWhereConditionsToQueryInnerWhereTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a LEFT JOIN (SELECT o.Psi FROM Omega WHERE o.Psi IS NOT NULL) io ON io.Psi = a.Gamma";
			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" }
				}
			};

			var queryConditions = new List<Expression<Func<Alpha, bool>>>
			{
				alpha => alpha.Gamma == "One"
			};

			// Act
			var result = queryConditions.AddWhereConditionsToQuery(query, data);

			// Assert
			result.QueryParameter.Should().HaveCount(1);
			result.QueryParameter.Keys.First().Should().Be("p0");
			result.QueryParameter.Values.First().Should().Be("One");

			result.Sql.Should().Be($"{query}{Environment.NewLine}WHERE{Environment.NewLine}(a.Gamma = @p0){Environment.NewLine}");
		}

		[TestMethod]
		public void CalculateSortConditionsTest()
		{
			// Arrange
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" },
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.CalculateSortConditions(data);

			// Assert
			result.Should().Be("a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void CalculateSortConditionsMapToAnotherAliasTest()
		{
			// Arrange
			var sortingEngine = new SortingQueryEngine();

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Gamma),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" },
					{ ".Gamma", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions<Alpha>(queryParameters, null);

			// Act
			var result = orderByConditions.Data.CalculateSortConditions(data);

			// Assert
			result.Should().Be("a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryNoOrderByTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a";
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" },
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be($"{query}{Environment.NewLine}ORDER BY{Environment.NewLine}a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryNoOrderByWithPropertyTypeAndPropertyMappingTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a";
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" }
				},
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be($"{query}{Environment.NewLine}ORDER BY{Environment.NewLine}a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryNoOrderWithPropertyTypeMappingByTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a";
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyTypeMappings = new Dictionary<Type, string>
				{
					{ typeof(Alpha), "a" },
					{ typeof(Omega), "o" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be($"{query}{Environment.NewLine}ORDER BY{Environment.NewLine}a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryWithOrderByTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a ORDER BY a.Gamma ASC";
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" },
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be($"{query}{Environment.NewLine}, {Environment.NewLine}a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryInnerOrderByTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a JOIN (SELECT * FROM Omega ORDER BY Psi) o ON o.Psi = a.Gamma";
			var sortingEngine = new SortingQueryEngine();
			var mappings = new Dictionary<string, List<Expression<Func<Alpha, object>>>>
			{
				{ nameof(Omega.Psi),  new List<Expression<Func<Alpha, object>>> { a => a.Omega.Psi } }
			};

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>
				{
					new SortingQueryProperty
					{
						PropertyName = nameof(Alpha.Beta),
						SortOrder = SortOrder.Descending
					},
					new SortingQueryProperty
					{
						PropertyName = nameof(Omega.Psi),
						SortOrder = SortOrder.Ascending
					}
				}
			};

			var data = new WhereQuerySettings
			{
				PropertyMappings = new Dictionary<string, string>
				{
					{ ".", "a" },
					{ ".Omega.Psi", "o.Psi" }
				}
			};

			var orderByConditions = sortingEngine.BuildSortConditions(queryParameters, mappings);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be($"{query}{Environment.NewLine}ORDER BY{Environment.NewLine}a.Beta DESC, o.Psi ASC");
		}

		[TestMethod]
		public void AddSortConditionsToQueryNoConditionsTest()
		{
			// Arrange
			var query = "SELECT * FROM Alpha a ORDER BY a.Gamma ASC";
			var sortingEngine = new SortingQueryEngine();

			var queryParameters = new QueryParameters
			{
				SortingQueryProperties = new List<SortingQueryProperty>()
			};

			var data = new WhereQuerySettings();

			var orderByConditions = sortingEngine.BuildSortConditions<Alpha>(queryParameters);

			// Act
			var result = orderByConditions.Data.AddSortConditionsToQuery(query, data);

			// Assert
			result.Should().Be(query);
		}
	}
}