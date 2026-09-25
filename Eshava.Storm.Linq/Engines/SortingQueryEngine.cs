using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eshava.Core.Linq.Models;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;

namespace Eshava.Storm.Linq.Engines
{
	internal class SortingQueryEngine : AbstractQueryEngine
	{
		private const string SQL_ORDERBY = "ORDER BY";

		public string AddSortConditionsToQuery(IEnumerable<OrderByCondition> orderByConditions, string sqlQuery, QuerySettings settings)
		{
			if (orderByConditions == default || !orderByConditions.Any())
			{
				return sqlQuery;
			}

			var orderBy = CalculateSortConditions(orderByConditions, settings);

			if (sqlQuery.LastIndexOfTopLevelKeyword(SQL_ORDERBY) >= 0)
			{
				orderBy = String.Join(Environment.NewLine, sqlQuery, ", ", orderBy);
			}
			else
			{
				orderBy = String.Join(Environment.NewLine, sqlQuery, SQL_ORDERBY, orderBy);
			}

			return orderBy;
		}

		public string CalculateSortConditions(IEnumerable<OrderByCondition> orderByConditions, QuerySettings settings)
		{
			if (!(orderByConditions?.Any() ?? false))
			{
				return "";
			}

			var data = new WhereQueryData
			{
				PropertyMappings = settings?.PropertyMappings ?? new Dictionary<string, string>(),
				PropertyTypeMappings = settings?.PropertyTypeMappings ?? new Dictionary<Type, string>(),
				QueryParameter = new Dictionary<string, object>()
			};

			var sql = new StringBuilder();

			foreach (var orderByCondition in orderByConditions)
			{
				if (sql.Length > 0)
				{
					sql.Append(", ");
				}

				var member = MapPropertyPath(data, ProcessExpression(orderByCondition.Member, data,System.Linq.Expressions.ExpressionType.Default));
				sql.Append(member);
				sql.Append(" ");
				sql.Append(orderByCondition.SortOrder == Core.Linq.Enums.SortOrder.Ascending ? "ASC" : "DESC");

			}

			return sql.ToString();
		}
	}
}