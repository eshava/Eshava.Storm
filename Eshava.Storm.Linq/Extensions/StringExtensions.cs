using System;
using System.Collections.Generic;
using System.Linq;

namespace Eshava.Storm.Linq.Extensions
{
	public static class StringExtensions
	{
		public static string AppendSkipAndTake(this string sqlQuery, int skip, int take)
		{
			if (take <= 0)
			{
				return sqlQuery;
			}

			return $"{sqlQuery}{Environment.NewLine}OFFSET {Math.Max(skip, 0)} ROWS FETCH NEXT {take} ROWS ONLY";
		}

		internal static bool IsNullOrEmpty(this string text)
		{
			return String.IsNullOrEmpty(text);
		}

		/// <summary>
		/// The position of the last occurrence of a keyword that belongs to the statement itself: a whole word,
		/// outside parentheses, string literals and quoted names
		/// </summary>
		internal static int LastIndexOfTopLevelKeyword(this string query, string keyword)
		{
			var lastIndex = -1;
			foreach (var index in GetTopLevelKeywordIndexes(query, new[] { keyword }, 0))
			{
				lastIndex = index;
			}

			return lastIndex;
		}

		/// <summary>
		/// The position of the first top level occurrence of any of the keywords, from the start index on
		/// </summary>
		internal static int IndexOfFirstTopLevelKeyword(this string query, IEnumerable<string> keywords, int startIndex)
		{
			foreach (var index in GetTopLevelKeywordIndexes(query, keywords.ToList(), startIndex))
			{
				return index;
			}

			return -1;
		}

		private static IEnumerable<int> GetTopLevelKeywordIndexes(string query, IList<string> keywords, int startIndex)
		{
			var depth = 0;
			var closingQuote = default(char?);

			for (var index = 0; index < query.Length; index++)
			{
				var character = query[index];

				if (closingQuote.HasValue)
				{
					if (character == closingQuote.Value)
					{
						closingQuote = null;
					}

					continue;
				}

				switch (character)
				{
					case '\'':
						closingQuote = '\'';
						continue;
					case '"':
						closingQuote = '"';
						continue;
					case '[':
						closingQuote = ']';
						continue;
					case '(':
						depth++;
						continue;
					case ')':
						depth--;
						continue;
				}

				if (depth != 0 || index < startIndex || (index > 0 && IsWordCharacter(query[index - 1])))
				{
					continue;
				}

				foreach (var keyword in keywords)
				{
					if (IsKeywordAt(query, index, keyword))
					{
						yield return index;

						break;
					}
				}
			}
		}

		private static bool IsKeywordAt(string query, int index, string keyword)
		{
			var parts = keyword.Split(' ');
			var position = index;

			foreach (var part in parts)
			{
				while (position < query.Length && position > index && Char.IsWhiteSpace(query[position]))
				{
					position++;
				}

				if (String.Compare(query, position, part, 0, part.Length, StringComparison.OrdinalIgnoreCase) != 0)
				{
					return false;
				}

				position += part.Length;
				if (position < query.Length && IsWordCharacter(query[position]))
				{
					return false;
				}
			}

			return true;
		}

		private static bool IsWordCharacter(char character)
		{
			return Char.IsLetterOrDigit(character) || character == '_' || character == '@' || character == '#' || character == '$' || character == '.';
		}
	}
}