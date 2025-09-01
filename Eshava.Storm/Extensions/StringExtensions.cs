using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Eshava.Storm.Constants;
using Eshava.Storm.Models;

namespace Eshava.Storm.Extensions
{
	internal static class StringExtensions
	{
		internal static bool IsNullOrEmpty(this string text)
		{
			return String.IsNullOrEmpty(text);
		}

		internal static string Clean(this string name)
		{
			if (!IsNullOrEmpty(name))
			{
				switch (name[0])
				{
					case '@':
					case ':':
					case '?':
						return name.Substring(1);
				}
			}

			return name;
		}

		internal static IEnumerable<LiteralToken> GetLiteralTokens(this string sql)
		{
			if (sql.IsNullOrEmpty())
			{
				return LiteralToken.None;
			}

			if (!RegExStrings.LiteralTokens.IsMatch(sql))
			{
				return LiteralToken.None;
			}

			var matches = RegExStrings.LiteralTokens.Matches(sql);
			var found = new HashSet<string>(StringComparer.Ordinal);
			var list = new List<LiteralToken>(matches.Count);

			foreach (Match match in matches)
			{
				var token = match.Value;
				if (found.Add(match.Value))
				{
					list.Add(new LiteralToken(token, match.Groups[1].Value));
				}
			}

			return list.Count == 0
				? LiteralToken.None
				: list;
		}

		internal static Dictionary<string, IList<string>> GetTableAliases(this string sql)
		{
			var tableAliases = new Dictionary<string, IList<string>>();

			if (sql.IsNullOrEmpty())
			{
				return tableAliases;
			}

			var hasMatchesAliases = RegExStrings.TablesAliases.IsMatch(sql);
			var hasMatchesSelectJoinsAliases = RegExStrings.SelectJoinAliases.IsMatch(sql);
			var hasMatchesSelectJoinsAliasesWithAS = RegExStrings.SelectJoinAliasesWithAs.IsMatch(sql);


			if (!hasMatchesAliases)
			{
				return tableAliases;
			}

			if (hasMatchesSelectJoinsAliases)
			{
				var matches = RegExStrings.SelectJoinAliasesInclusiveInnerSelect.Matches(sql);
				ExecuteSelectJoinRegEx(matches, 5, 4, tableAliases);
			}

			if (hasMatchesSelectJoinsAliasesWithAS)
			{
				var matches = RegExStrings.SelectJoinAliasesWithAsInclusiveInnerSelect.Matches(sql);
				ExecuteSelectJoinRegEx(matches, 6, 3, tableAliases);
			}

			if (hasMatchesAliases)
			{
				var matches = RegExStrings.TablesAliases.Matches(sql);
				ExecuteTableRegEx(matches, 4, tableAliases);
			}

			return tableAliases;
		}

		private static void ExecuteSelectJoinRegEx(MatchCollection matches, int queryAliasGroup, int secondGroupIndex, Dictionary<string, IList<string>> tableAliases)
		{
			foreach (Match match in matches)
			{
				var query = match.Groups[3].Value;
				var queryMatches = RegExStrings.TablesAliases.Matches(query);
				var innerTableAliases = new Dictionary<string, IList<string>>();
				ExecuteTableRegEx(queryMatches, secondGroupIndex, innerTableAliases);

				var alias = match.Groups[queryAliasGroup].Value.CleanTableAlias();
				if (innerTableAliases.Count == 0)
				{
					tableAliases.Add(alias, new List<string> { "*" });
				}
				else
				{
					tableAliases.Add(alias, innerTableAliases.Values.SelectMany(v => v).ToList());
				}
			}
		}

		private static void ExecuteTableRegEx(MatchCollection matches, int secondGroupIndex, Dictionary<string, IList<string>> tableAliases)
		{
			foreach (Match match in matches)
			{
				(var tableSchema, var tableName) = match.Groups["table"].Value.CleanTableName();
				var alias = match.Groups["tablealias"].Value.CleanTableAlias();

				if (alias.IsNullOrEmpty())
				{
					continue;
				}

				if (!tableAliases.ContainsKey(alias))
				{
					tableAliases.Add(alias, new List<string> { $"{tableSchema}.{tableName}" });
				}
			}
		}

		internal static (string Schema, string Table) CleanTableName(this string tableName)
		{
			var tableParts = tableName.ToLower()
				.Replace("[", "")
				.Replace("]", "")
				.Trim()
				.Split('.');

			return tableParts.Length == 1
				? ("dbo", tableParts[0])
				: (tableParts[tableParts.Length - 2], tableParts[tableParts.Length - 1]);
		}

		internal static string CleanTableAlias(this string tableName)
		{
			return tableName
				.ToLower()
				.Replace("[", "")
				.Replace("]", "")
				.Trim()
				.Split('.')
				.Last()
				;
		}
	}
}