using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Eshava.Storm.Linq.Enums;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;
using Eshava.Storm.Linq.Visitors;

namespace Eshava.Storm.Linq.Engines
{
	internal abstract class AbstractQueryEngine
	{
		private const string PARAMETER_PLACEHOLDER = "###";
		private const string METHOD_CONTAINS = "contains";
		private const string METHOD_ANY = "any";
		private const string METHOD_STARTSWITH = "startswith";
		private const string METHOD_ENDSWITH = "endswith";
		private const string METHOD_COMPARETO = "compareto";
		private const string METHOD_TOUPPER = "toupper";
		private const string METHOD_TOLOWER = "tolower";
		private const string SQL_TRUE = "(1 = 1)";
		private const string SQL_FALSE = "(1 = 0)";
		private const string SQL_LOWER = "lower";
		private const string SQL_UPPER = "upper";

		protected const string SQL_AND = "AND";

		private static Dictionary<ExpressionType, string> _expressionTypeMappings = new Dictionary<ExpressionType, string>
		{
			{  ExpressionType.And, SQL_AND },
			{  ExpressionType.AndAlso, SQL_AND },
			{  ExpressionType.Equal, "=" },
			{  ExpressionType.NotEqual, "!=" },
			{  ExpressionType.GreaterThan, ">" },
			{  ExpressionType.GreaterThanOrEqual, ">=" },
			{  ExpressionType.LessThan, "<" },
			{  ExpressionType.LessThanOrEqual, "<=" },
			{  ExpressionType.Or, "OR" },
			{  ExpressionType.OrElse, "OR" }
		};

		/// <summary>
		/// Translates a condition that stands on its own: a value that is known up front becomes a literal predicate
		/// </summary>
		protected string ProcessCondition(Expression expression, WhereQueryData data)
		{
			if (IsEvaluable(expression) && expression.Type == typeof(bool))
			{
				return (bool)Evaluate(expression) ? SQL_TRUE : SQL_FALSE;
			}

			return ProcessExpression(expression, data, null);
		}

		protected string ProcessExpression(Expression expression, WhereQueryData data, ExpressionType? parentExpressionType)
		{
			// A part of the expression that does not depend on the lambda parameter is a value: it is evaluated once
			// and passed as a parameter, whatever it is made of — a captured variable, a member of a captured object,
			// a static member or a method call
			if (IsEvaluable(expression))
			{
				return ProcessConstantExpression(Evaluate(expression), data);
			}

			var unaryExpression = expression as UnaryExpression;
			if (unaryExpression != default && expression.NodeType == ExpressionType.Not)
			{
				return ProcessUnaryExpressionNot(unaryExpression, data);
			}

			if (unaryExpression != default && (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked))
			{
				return ProcessExpression(unaryExpression.Operand, data, unaryExpression.NodeType);
			}

			var binaryExpression = expression as BinaryExpression;
			if (binaryExpression != default)
			{
				return ProcessBinaryExpression(binaryExpression, data);
			}

			var memberExpression = expression as MemberExpression;
			if (memberExpression != default)
			{
				var result = ProcessMemberExpression(memberExpression, data);

				return CheckMemberExpressionBooleanPropertyIssue(data, parentExpressionType, result);
			}

			var methodCallExpression = expression as MethodCallExpression;
			if (methodCallExpression != default)
			{
				return ProcessMethodCallExpression(methodCallExpression, data);
			}

			if (expression.NodeType == ExpressionType.Parameter)
			{
				return ProcessParameterExpression(expression as ParameterExpression, data);
			}

			throw new NotSupportedException($"The expression {expression} cannot be translated to SQL.");
		}

		protected string MapPropertyPath(QuerySettings data, string propertyName)
		{
			if (propertyName.IsNullOrEmpty())
			{
				return propertyName;
			}

			var propertyNameModified = propertyName.StartsWith(".") || !propertyName.Contains(".")
				? propertyName
				: propertyName.Substring(propertyName.IndexOf("."))
				;

			if (!propertyName.StartsWith(".")
				&& !data.PropertyMappings.ContainsKey(propertyName)
				&& !data.PropertyMappings.ContainsKey(propertyNameModified))
			{
				return propertyName;
			}

			if (data.PropertyMappings.ContainsKey(propertyName))
			{
				return data.PropertyMappings[propertyName];
			}

			if (data.PropertyMappings.ContainsKey(propertyNameModified))
			{
				return data.PropertyMappings[propertyNameModified];
			}

			if (data.PropertyMappings.ContainsKey("."))
			{
				return data.PropertyMappings["."] + propertyName;
			}

			return propertyName.Substring(1);
		}

		private string ProcessBinaryExpression(BinaryExpression binaryExpression, WhereQueryData data)
		{
			if (!_expressionTypeMappings.ContainsKey(binaryExpression.NodeType))
			{
				throw new NotSupportedException($"The operator {binaryExpression.NodeType} in {binaryExpression} cannot be translated to SQL.");
			}

			var nodeType = _expressionTypeMappings[binaryExpression.NodeType];

			if (IsCombinationType(binaryExpression.NodeType))
			{
				var leftCondition = ProcessCombinationOperand(binaryExpression.Left, data);
				var rightCondition = ProcessCombinationOperand(binaryExpression.Right, data);

				return $"({leftCondition} {nodeType} {rightCondition})";
			}

			var left = ProcessExpression(binaryExpression.Left, data, ExpressionType.Default);
			var right = ProcessExpression(binaryExpression.Right, data, ExpressionType.Default);

			// NULL on either side is a test for NULL
			if (left == null && right != null)
			{
				(left, right) = (right, left);
			}

			if (right == null && (binaryExpression.NodeType == ExpressionType.Equal || binaryExpression.NodeType == ExpressionType.NotEqual))
			{
				if (left == null)
				{
					return binaryExpression.NodeType == ExpressionType.Equal ? SQL_TRUE : SQL_FALSE;
				}

				return $"({MapPropertyPath(data, left)} {(binaryExpression.NodeType == ExpressionType.Equal ? "IS" : "IS NOT")} NULL)";
			}

			if (left == null || right == null)
			{
				throw new NotSupportedException($"NULL cannot be compared with {binaryExpression.NodeType} in {binaryExpression}.");
			}

			return $"({MapPropertyPath(data, left)} {nodeType} {MapPropertyPath(data, right)})";
		}

		private string ProcessCombinationOperand(Expression operand, WhereQueryData data)
		{
			if (IsEvaluable(operand) && operand.Type == typeof(bool))
			{
				return (bool)Evaluate(operand) ? SQL_TRUE : SQL_FALSE;
			}

			// A boolean member standing on its own is completed to a comparison, "p => p.IsActive" means "p.IsActive = true"
			return ProcessExpression(operand, data, operand.NodeType == ExpressionType.MemberAccess ? null : ExpressionType.Default);
		}

		private string ProcessMemberExpression(MemberExpression memberExpression, WhereQueryData data)
		{
			// Nullable<T>.Value of a column is the column itself
			if (memberExpression.Member.Name == nameof(Nullable<int>.Value)
				&& memberExpression.Member.DeclaringType.IsGenericType
				&& memberExpression.Member.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				return ProcessExpression(memberExpression.Expression, data, memberExpression.NodeType);
			}

			// The member depends on the lambda parameter, otherwise it would have been evaluated,
			// so a type mapping names the table of this member and never that of a captured value
			var memberDataType = GetDataType(memberExpression.Member);
			if (memberDataType != default && data.PropertyTypeMappings.ContainsKey(memberDataType))
			{
				return data.PropertyTypeMappings[memberDataType];
			}

			var parent = ProcessExpression(memberExpression.Expression, data, memberExpression.NodeType);

			return $"{parent}.{memberExpression.Member.Name}";
		}

		private string ProcessParameterExpression(ParameterExpression parameterExpression, WhereQueryData data)
		{
			if (parameterExpression == data.InnerParameter)
			{
				return PARAMETER_PLACEHOLDER;
			}

			if (data.PropertyTypeMappings.ContainsKey(parameterExpression.Type))
			{
				return data.PropertyTypeMappings[parameterExpression.Type];
			}

			return "";
		}

		private string ProcessUnaryExpressionNot(UnaryExpression unaryExpression, WhereQueryData data)
		{
			var expressionResult = ProcessExpression(unaryExpression.Operand, data, unaryExpression.NodeType);

			if (!expressionResult.IsNullOrEmpty() && expressionResult.StartsWith("(") && expressionResult.EndsWith(")"))
			{
				return $"NOT{expressionResult}";
			}

			return $"NOT({expressionResult})";
		}

		private string ProcessConstantExpression(object value, WhereQueryData data)
		{
			if (value == null)
			{
				return null;
			}

			// Through the underlying type, so an enum based on long keeps its full range
			if (value.GetType().IsEnum)
			{
				value = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
			}

			return "@" + AddParameter(data, value);
		}

		private string ProcessMethodCallExpression(MethodCallExpression methodCallExpression, WhereQueryData data)
		{
			var method = methodCallExpression.Method.Name.ToLowerInvariant();

			if (method == METHOD_ANY)
			{
				return ProcessMethodCallExpressionAny(methodCallExpression, data);
			}

			if (method == METHOD_TOUPPER || method == METHOD_TOLOWER)
			{
				var column = ProcessExpression(methodCallExpression.Object, data, methodCallExpression.NodeType);

				// Ignored, the case is left to the collation of the column, unless the settings ask for a translation
				return LinqSettings.IsCaseConversionTranslated
					? $"{(method == METHOD_TOLOWER ? SQL_LOWER : SQL_UPPER)}({MapPropertyPath(data, column)})"
					: column;
			}

			if (methodCallExpression.Object is null)
			{
				// Enumerable.Contains(values, p.Property), and since C# 14 MemoryExtensions.Contains(values, p.Property, comparer) for arrays
				if (method == METHOD_CONTAINS && methodCallExpression.Arguments.Count >= 2)
				{
					return ProcessContainedIn(methodCallExpression.Arguments[0], methodCallExpression.Arguments[1], data);
				}

				throw new NotSupportedException($"The method {methodCallExpression.Method.DeclaringType?.Name}.{methodCallExpression.Method.Name} in {methodCallExpression} cannot be translated to SQL.");
			}

			if (method == METHOD_CONTAINS && IsEvaluable(methodCallExpression.Object))
			{
				if (methodCallExpression.Object.Type == typeof(string))
				{
					throw new NotSupportedException($"A column cannot be searched for inside a value, as in {methodCallExpression}.");
				}

				// values.Contains(p.Property) on a list
				return ProcessContainedIn(methodCallExpression.Object, methodCallExpression.Arguments[0], data);
			}

			var likeOperator = "LIKE";
			var searchedExpression = methodCallExpression.Object;

			// On PostgreSQL a search in a lowered or uppered column is ILIKE on the column itself
			if (IsLikeMethod(method)
				&& LinqSettings.IsCaseConversionTranslated
				&& LinqSettings.Dialect == QueryDialect.PostgreSql
				&& IsCaseConversion(searchedExpression, out var convertedColumn))
			{
				likeOperator = "ILIKE";
				searchedExpression = convertedColumn;
			}

			var property = MapPropertyPath(data, ProcessExpression(searchedExpression, data, methodCallExpression.NodeType));

			switch (method)
			{
				case METHOD_CONTAINS:
					return $"{property} {likeOperator} {GetLikeParameter(methodCallExpression, data, value => $"%{value}%")}{GetLikeEscapeClause()}";
				case METHOD_STARTSWITH:
					return $"{property} {likeOperator} {GetLikeParameter(methodCallExpression, data, value => $"{value}%")}{GetLikeEscapeClause()}";
				case METHOD_ENDSWITH:
					return $"{property} {likeOperator} {GetLikeParameter(methodCallExpression, data, value => $"%{value}")}{GetLikeEscapeClause()}";
				case METHOD_COMPARETO:
					// EF Core compatibility
					var compareValue = ProcessExpression(methodCallExpression.Arguments[0], data, methodCallExpression.NodeType);

					return $"(CASE WHEN {property} = {compareValue} THEN 0 WHEN {property} > {compareValue} THEN 1 ELSE -1 END)";
			}

			throw new NotSupportedException($"The method {methodCallExpression.Method.Name} in {methodCallExpression} cannot be translated to SQL.");
		}

		private string ProcessContainedIn(Expression valuesExpression, Expression itemExpression, WhereQueryData data)
		{
			valuesExpression = UnwrapSpanConversion(valuesExpression);
			if (!IsEvaluable(valuesExpression))
			{
				throw new NotSupportedException($"Only a list of values can be searched, not {valuesExpression}.");
			}

			var values = Evaluate(valuesExpression) as IEnumerable;
			if (values == null || !values.Cast<object>().Any())
			{
				// Nothing is contained in an empty list
				return SQL_FALSE;
			}

			var property = MapPropertyPath(data, ProcessExpression(itemExpression, data, ExpressionType.Call));
			var parameterName = AddParameter(data, values, "Array");

			return $"{property} IN @{parameterName}";
		}

		private string GetLikeParameter(MethodCallExpression methodCallExpression, WhereQueryData data, Func<string, string> pattern)
		{
			var argument = methodCallExpression.Arguments[0];
			if (!IsEvaluable(argument))
			{
				throw new NotSupportedException($"The search term of {methodCallExpression} has to be a value, not a column.");
			}

			var value = Evaluate(argument);
			if (value == null)
			{
				throw new ArgumentNullException(nameof(methodCallExpression), $"The search term of {methodCallExpression} is null.");
			}

			return "@" + AddParameter(data, pattern(EscapeLikeValue(value.ToString())));
		}

		/// <summary>
		/// The value is searched for as it is: the wildcards of LIKE in it are escaped. SQL Server knows the bracket
		/// syntax; PostgreSQL uses the backslash, its default escape character; SQLite has neither by default and
		/// is given the backslash through an ESCAPE clause.
		/// </summary>
		private static string EscapeLikeValue(string value)
		{
			if (LinqSettings.Dialect == QueryDialect.SqlServer)
			{
				return value
					.Replace("[", "[[]")
					.Replace("%", "[%]")
					.Replace("_", "[_]");
			}

			return value
				.Replace("\\", "\\\\")
				.Replace("%", "\\%")
				.Replace("_", "\\_");
		}

		private static string GetLikeEscapeClause()
		{
			return LinqSettings.Dialect == QueryDialect.Sqlite ? " ESCAPE '\\'" : "";
		}

		private static bool IsLikeMethod(string method)
		{
			return method == METHOD_CONTAINS || method == METHOD_STARTSWITH || method == METHOD_ENDSWITH;
		}

		private static bool IsCaseConversion(Expression expression, out Expression column)
		{
			column = null;
			if (expression is MethodCallExpression methodCallExpression
				&& methodCallExpression.Object != null
				&& methodCallExpression.Arguments.Count == 0
				&& (methodCallExpression.Method.Name == nameof(String.ToLower) || methodCallExpression.Method.Name == nameof(String.ToUpper)))
			{
				column = methodCallExpression.Object;
			}

			return column != null;
		}

		private string ProcessMethodCallExpressionAny(MethodCallExpression methodCallExpression, WhereQueryData data)
		{
			var source = UnwrapSpanConversion(methodCallExpression.Arguments.First());
			if (!IsEvaluable(source))
			{
				throw new NotSupportedException($"Any can only be translated over a list of values, not over {source}.");
			}

			var lambdaExpression = methodCallExpression.Arguments.Last() as LambdaExpression;
			if (lambdaExpression == null)
			{
				throw new NotSupportedException($"Any can only be translated with a lambda expression, as in {methodCallExpression}.");
			}

			var items = (Evaluate(source) as IEnumerable)?.Cast<object>().ToList() ?? new List<object>();
			if (items.Count == 0)
			{
				// Any over an empty list is false
				return SQL_FALSE;
			}

			var innerParameter = lambdaExpression.Parameters[0];
			var queryParts = new List<string>();

			if (ParameterUsageCounter.IsOnlyComparedDirectly(lambdaExpression.Body, innerParameter))
			{
				// Translated once; each item gets one parameter that replaces every use of the lambda parameter
				data.InnerParameter = innerParameter;
				var lambdaAsQuery = ProcessCondition(lambdaExpression.Body, data);
				data.InnerParameter = null;

				foreach (var item in items)
				{
					queryParts.Add(lambdaAsQuery.Replace(PARAMETER_PLACEHOLDER, "@" + AddParameter(data, item)));
				}
			}
			else
			{
				// The item is used in another way, v => v.Id == p.Id: the lambda is translated per item
				foreach (var item in items)
				{
					var body = ParameterReplacer.Replace(lambdaExpression.Body, innerParameter, Expression.Constant(item, innerParameter.Type));
					queryParts.Add(ProcessCondition(body, data));
				}
			}

			return $"({String.Join(" OR ", queryParts)})";
		}

		/// <summary>
		/// Adds a parameter under the next name the query parameters do not use yet
		/// </summary>
		private static string AddParameter(WhereQueryData data, object value, string suffix = "")
		{
			string parameterName;
			do
			{
				parameterName = "p" + data.Index + suffix;
				data.Index++;
			}
			while (data.QueryParameter.ContainsKey(parameterName));

			data.QueryParameter.Add(parameterName, value);

			return parameterName;
		}

		/// <summary>
		/// Since C# 14 an array passed to Contains is converted implicitly into a span, which cannot be boxed and evaluated;
		/// the array itself is the list of values
		/// </summary>
		private static Expression UnwrapSpanConversion(Expression expression)
		{
			while (IsSpanConversion(expression))
			{
				expression = ((MethodCallExpression)expression).Arguments[0];
			}

			return expression;
		}

		private static bool IsSpanConversion(Expression expression)
		{
			return expression is MethodCallExpression methodCallExpression
				&& methodCallExpression.Method.Name == "op_Implicit"
				&& methodCallExpression.Arguments.Count == 1
				&& (methodCallExpression.Type.Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal) || methodCallExpression.Type.Name.StartsWith("Span", StringComparison.Ordinal));
		}

		private static bool IsEvaluable(Expression expression)
		{
			return expression.NodeType != ExpressionType.Lambda
				&& expression.NodeType != ExpressionType.Quote
				&& !ParameterFinder.ContainsParameter(expression);
		}

		private static object Evaluate(Expression expression)
		{
			switch (expression)
			{
				case ConstantExpression constantExpression:
					return constantExpression.Value;
				case MemberExpression memberExpression when memberExpression.Member is FieldInfo fieldInfo:
					return fieldInfo.GetValue(memberExpression.Expression == null ? null : Evaluate(memberExpression.Expression));
				case MemberExpression memberExpression when memberExpression.Member is PropertyInfo propertyInfo:
					return propertyInfo.GetValue(memberExpression.Expression == null ? null : Evaluate(memberExpression.Expression));
			}

			return Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile()();
		}

		private Type GetDataType(MemberInfo memberInfo)
		{
			if (memberInfo.MemberType == MemberTypes.Field)
			{
				return ((FieldInfo)memberInfo).FieldType;
			}
			else if (memberInfo.MemberType == MemberTypes.Property)
			{
				return ((PropertyInfo)memberInfo).PropertyType;
			}

			return null;
		}

		private string CheckMemberExpressionBooleanPropertyIssue(WhereQueryData data, ExpressionType? parentExpressionType, string result)
		{
			if (parentExpressionType == ExpressionType.Convert
				|| parentExpressionType == ExpressionType.Default
				|| parentExpressionType == ExpressionType.MemberAccess
				|| parentExpressionType == ExpressionType.Call)
			{
				return result;
			}

			// the member expression is properly a boolean expression like "p => p.PropertyName",
			// so that the expression have to be transformed to "p => p.PropertyName == true"
			return $"({MapPropertyPath(data, result)} = @{AddParameter(data, true)})";
		}

		private static bool IsCombinationType(ExpressionType expressionType)
		{
			return expressionType == ExpressionType.And
				|| expressionType == ExpressionType.AndAlso
				|| expressionType == ExpressionType.Or
				|| expressionType == ExpressionType.OrElse;
		}
	}
}