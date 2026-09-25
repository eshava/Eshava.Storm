using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;

namespace Eshava.Storm.Linq.Engines
{
	internal class WhereQueryEngine : AbstractQueryEngine
	{
		private const string SQL_WHERE = "WHERE";

		// The clauses that follow WHERE; new conditions are inserted before the first of them
		private static readonly string[] _clausesAfterWhere = new[] { "GROUP BY", "HAVING", "WINDOW", "ORDER BY", "OFFSET", "OPTION", "FOR XML", "FOR JSON", "FOR BROWSE" };

		public WhereQueryResult AddWhereConditionsToQuery<T>(IEnumerable<Expression<Func<T, bool>>> queryConditions, string sqlQuery, WhereQuerySettings settings)
		{
			var whereQueryResult = CalculateWhereConditions(queryConditions, settings);

			if (whereQueryResult.Sql.IsNullOrEmpty())
			{
				whereQueryResult.Sql = sqlQuery;

				return whereQueryResult;
			}

			var whereIndex = sqlQuery.LastIndexOfTopLevelKeyword(SQL_WHERE);
			var searchStart = Math.Max(whereIndex, sqlQuery.LastIndexOfTopLevelKeyword("SELECT"));
			var insertIndex = sqlQuery.IndexOfFirstTopLevelKeyword(_clausesAfterWhere, Math.Max(searchStart, 0));
			if (insertIndex < 0)
			{
				insertIndex = sqlQuery.Length;
			}

			var queryStart = sqlQuery.Substring(0, insertIndex).TrimEnd();
			var queryEnd = sqlQuery.Substring(insertIndex);

			if (whereIndex >= 0)
			{
				// AND binds tighter than OR: an existing condition with a top-level OR is enclosed, or the new
				// conditions would apply to its last part only
				var conditionStart = whereIndex + SQL_WHERE.Length;
				var existingCondition = queryStart.Substring(conditionStart);
				if (existingCondition.LastIndexOfTopLevelKeyword("OR") >= 0)
				{
					queryStart = $"{queryStart.Substring(0, conditionStart)} ({existingCondition.Trim()})";
				}

				whereQueryResult.Sql = String.Join(Environment.NewLine, queryStart, SQL_AND, whereQueryResult.Sql) + queryEnd;
			}
			else
			{
				whereQueryResult.Sql = String.Join(Environment.NewLine, queryStart, SQL_WHERE, whereQueryResult.Sql) + queryEnd;
			}

			return whereQueryResult;
		}

		public WhereQueryResult CalculateWhereConditions<T>(IEnumerable<Expression<Func<T, bool>>> queryConditions, WhereQuerySettings settings)
		{
			// A copy: the settings can be used for more than one query, and the parameters of one must not leak into the next
			var queryParameter = settings?.QueryParameter == null
				? new Dictionary<string, object>()
				: new Dictionary<string, object>(settings.QueryParameter);

			var result = new WhereQueryResult(queryParameter);

			if (!(queryConditions?.Any() ?? false))
			{
				return result;
			}

			var data = new WhereQueryData
			{
				PropertyMappings = settings?.PropertyMappings ?? new Dictionary<string, string>(),
				PropertyTypeMappings = settings?.PropertyTypeMappings ?? new Dictionary<Type, string>(),
				QueryParameter = result.QueryParameter
			};

			var sql = new StringBuilder();
			foreach (var queryCondition in queryConditions)
			{
				if (sql.Length > 0)
				{
					sql.AppendLine("AND");
				}

				sql.AppendLine(ProcessCondition(queryCondition.Body, data));
			}

			result.Sql = sql.ToString();

			return result;
		}
	}
}