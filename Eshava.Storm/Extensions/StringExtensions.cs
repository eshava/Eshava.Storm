using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Eshava.Storm.Constants;

namespace Eshava.Storm.Extensions
{
	internal static class StringExtensions
	{
		private static readonly char[] _whitespaces = new[] { ' ', '\t', '\r', '\n' };

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

		internal static Dictionary<string, IList<string>> GetTableAliases(this string sql)
		{
			var tableAliases = new Dictionary<string, IList<string>>();

			if (sql.IsNullOrEmpty())
			{
				return tableAliases;
			}

			if (RegExStrings.SelectJoinAliases.IsMatch(sql))
			{
				var matches = RegExStrings.SelectJoinAliasesInclusiveInnerSelect.Matches(sql);
				ExecuteSelectJoinRegEx(matches, 5, tableAliases);
			}

			if (RegExStrings.SelectJoinAliasesWithAs.IsMatch(sql))
			{
				var matches = RegExStrings.SelectJoinAliasesWithAsInclusiveInnerSelect.Matches(sql);
				ExecuteSelectJoinRegEx(matches, 6, tableAliases);
			}

			ExecuteTableRegEx(RegExStrings.TablesAliases.Matches(sql), tableAliases);
			AddFromClauseItems(sql, tableAliases);
			ResolveCommonTableExpressions(sql, tableAliases);

			return tableAliases;
		}

		private static void ExecuteSelectJoinRegEx(MatchCollection matches, int queryAliasGroup, Dictionary<string, IList<string>> tableAliases)
		{
			foreach (Match match in matches)
			{
				var alias = match.Groups[queryAliasGroup].Value.CleanTableAlias();
				AddTableNames(tableAliases, alias, GetReferencedTableNames(match.Groups[3].Value));
			}
		}

		private static void ExecuteTableRegEx(MatchCollection matches, Dictionary<string, IList<string>> tableAliases)
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

		/// <summary>
		/// Registers the items of every FROM clause the regular expressions do not cover:
		/// the tables after the first one in a comma separated list, and derived tables, <c>FROM (SELECT ...) x</c>
		/// </summary>
		private static void AddFromClauseItems(string sql, Dictionary<string, IList<string>> tableAliases)
		{
			foreach (Match fromMatch in RegExStrings.FromKeyword.Matches(sql))
			{
				foreach (var item in GetFromClauseItems(sql, fromMatch.Index + fromMatch.Length))
				{
					if (item.StartsWith("("))
					{
						var closingIndex = FindClosingParenthesis(item, 0);
						if (closingIndex < 0)
						{
							continue;
						}

						var derivedAlias = GetAliasFromTokens(item.Substring(closingIndex + 1).Split(_whitespaces, StringSplitOptions.RemoveEmptyEntries), 0);
						if (!derivedAlias.IsNullOrEmpty())
						{
							AddTableNames(tableAliases, derivedAlias.CleanTableAlias(), GetReferencedTableNames(item.Substring(1, closingIndex - 1)));
						}

						continue;
					}

					var tokens = item.Split(_whitespaces, StringSplitOptions.RemoveEmptyEntries);
					var alias = GetAliasFromTokens(tokens, 1);
					if (alias.IsNullOrEmpty())
					{
						continue;
					}

					alias = alias.CleanTableAlias();
					if (!tableAliases.ContainsKey(alias))
					{
						(var tableSchema, var tableName) = tokens[0].CleanTableName();
						tableAliases.Add(alias, new List<string> { $"{tableSchema}.{tableName}" });
					}
				}
			}
		}

		/// <summary>
		/// An alias pointing at a common table expression points at the tables that expression reads from
		/// </summary>
		private static void ResolveCommonTableExpressions(string sql, Dictionary<string, IList<string>> tableAliases)
		{
			foreach (Match match in RegExStrings.CommonTableExpression.Matches(sql))
			{
				var openingIndex = match.Index + match.Length - 1;
				var closingIndex = FindClosingParenthesis(sql, openingIndex);
				if (closingIndex < 0)
				{
					continue;
				}

				(var cteSchema, var cteName) = match.Groups["name"].Value.CleanTableName();
				var cteTableName = $"{cteSchema}.{cteName}";
				var innerTableNames = GetReferencedTableNames(sql.Substring(openingIndex + 1, closingIndex - openingIndex - 1))
					.Where(tableName => tableName != cteTableName)
					.ToList();

				if (innerTableNames.Count == 0)
				{
					continue;
				}

				foreach (var alias in tableAliases.Where(pair => pair.Value.Contains(cteTableName)).Select(pair => pair.Key).ToList())
				{
					tableAliases[alias] = tableAliases[alias]
						.Where(tableName => tableName != cteTableName)
						.Concat(innerTableNames)
						.Distinct()
						.ToList();
				}
			}
		}

		/// <summary>
		/// The tables a statement reads from, or <c>*</c> if none is recognised
		/// </summary>
		private static IList<string> GetReferencedTableNames(string sql)
		{
			var tableNames = RegExStrings.TableReferences.Matches(sql)
				.Cast<Match>()
				.Select(match => match.Groups["table"].Value.CleanTableName())
				.Select(table => $"{table.Schema}.{table.Table}")
				.Distinct()
				.ToList();

			return tableNames.Count == 0
				? new List<string> { "*" }
				: tableNames;
		}

		private static void AddTableNames(Dictionary<string, IList<string>> tableAliases, string alias, IList<string> tableNames)
		{
			if (alias.IsNullOrEmpty())
			{
				return;
			}

			// The same alias can be used in several parts of a statement, UNION for instance
			tableAliases[alias] = tableAliases.TryGetValue(alias, out var existingTableNames)
				? existingTableNames.Concat(tableNames).Distinct().ToList()
				: tableNames;
		}

		/// <summary>
		/// Splits a FROM clause into its comma separated items, starting after the keyword and ending
		/// at the first keyword or closing parenthesis on the same level
		/// </summary>
		private static IEnumerable<string> GetFromClauseItems(string sql, int startIndex)
		{
			var items = new List<string>();
			var depth = 0;
			var itemStart = startIndex;
			var index = startIndex;

			while (index < sql.Length)
			{
				var character = sql[index];
				if (character == '(')
				{
					depth++;
				}
				else if (character == ')')
				{
					if (depth == 0)
					{
						break;
					}

					depth--;
				}
				else if (depth == 0 && character == ',')
				{
					items.Add(sql.Substring(itemStart, index - itemStart).Trim());
					itemStart = index + 1;
				}
				else if (depth == 0 && character == ';')
				{
					break;
				}
				else if (depth == 0 && IsWordStart(sql, index))
				{
					var keywordMatch = RegExStrings.FromClauseEnd.Match(sql, index);
					if (keywordMatch.Success && keywordMatch.Index == index)
					{
						break;
					}
				}

				index++;
			}

			items.Add(sql.Substring(itemStart, index - itemStart).Trim());

			return items.Where(item => item.Length > 0);
		}

		private static string GetAliasFromTokens(IList<string> tokens, int index)
		{
			if (tokens.Count > index && String.Equals(tokens[index], "AS", StringComparison.OrdinalIgnoreCase))
			{
				index++;
			}

			if (tokens.Count <= index || RegExStrings.NoAlias.IsMatch(tokens[index]))
			{
				return null;
			}

			return tokens[index];
		}

		private static int FindClosingParenthesis(string sql, int openingIndex)
		{
			var depth = 0;
			for (var index = openingIndex; index < sql.Length; index++)
			{
				if (sql[index] == '(')
				{
					depth++;
				}
				else if (sql[index] == ')')
				{
					depth--;
					if (depth == 0)
					{
						return index;
					}
				}
			}

			return -1;
		}

		private static bool IsWordStart(string sql, int index)
		{
			return Char.IsLetter(sql[index]) && (index == 0 || !(Char.IsLetterOrDigit(sql[index - 1]) || sql[index - 1] == '_'));
		}

		/// <summary>
		/// The select list of the outermost statement: from its SELECT to its FROM, both on the top level,
		/// so neither a common table expression nor a subquery in the select list cuts it short
		/// </summary>
		internal static string GetMainSelectList(this string sql)
		{
			var depth = 0;
			var selectStart = -1;
			var inLiteral = false;

			for (var index = 0; index < sql.Length; index++)
			{
				var character = sql[index];
				if (character == '\'')
				{
					inLiteral = !inLiteral;
				}

				if (inLiteral)
				{
					continue;
				}

				if (character == '(')
				{
					depth++;
				}
				else if (character == ')')
				{
					depth--;
				}
				else if (depth == 0 && IsKeywordAt(sql, index, "select"))
				{
					selectStart = index;
				}
				else if (depth == 0 && selectStart >= 0 && IsKeywordAt(sql, index, "from"))
				{
					return sql.Substring(selectStart, index - selectStart);
				}
			}

			return selectStart >= 0 ? sql.Substring(selectStart) : sql;
		}

		private static bool IsKeywordAt(string sql, int index, string keyword)
		{
			return String.Compare(sql, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
				&& IsWordStart(sql, index)
				&& (index + keyword.Length == sql.Length || !(Char.IsLetterOrDigit(sql[index + keyword.Length]) || sql[index + keyword.Length] == '_'));
		}

		internal static (string Schema, string Table) CleanTableName(this string tableName)
		{
			var tableParts = tableName.ToLowerInvariant()
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
				.ToLowerInvariant()
				.Replace("[", "")
				.Replace("]", "")
				.Trim()
				.Split('.')
				.Last()
				;
		}
	}
}