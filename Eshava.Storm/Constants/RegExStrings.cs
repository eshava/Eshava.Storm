using System.Text.RegularExpressions;

namespace Eshava.Storm.Constants
{
	internal static class RegExStrings
	{
		public static readonly Regex TablesAliases = new Regex(@"(\bFROM\b|\bJOIN\b)\s+(?<table>\[?\b\S+\b\]?)(AS|FOR|SYSTEM_TIME|OF|@{1}\S*|\s)*\s+(?<tablealias>\[?(?!\b(ON|JOIN|INNER|OUTER|LEFT|RIGHT|CROSS|WHERE)\b)(\s{0}\S+)\]?)\s*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		
		public static readonly Regex SelectJoinAliases = new Regex(@"(\))\s+\[?(\b\S+\b)\]?\s+(\bON\b)\s+", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex SelectJoinAliasesWithAs = new Regex(@"(\))\s+(\bAS\b)\s+\[?(\b\S+\b)\]?\s+(\bON\b)\s+", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public static readonly Regex SelectJoinAliasesInclusiveInnerSelect = new Regex(@"(\bJOIN\b\s*)(\(){1}([\s\S]+?)(\))\s+\[?(\b\S+\b)\]?\s+(\bON\b)\s+", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex SelectJoinAliasesWithAsInclusiveInnerSelect = new Regex(@"(\bJOIN\b\s*)(\(){1}([\s\S]+?)(\))\s+(\bAS\b)\s+\[?(\b\S+\b)\]?\s+(\bON\b)\s+", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		// Used by the analysis of FROM clauses, derived tables and common table expressions
		public static readonly Regex FromKeyword = new Regex(@"\bFROM\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex FromClauseEnd = new Regex(@"\G(WHERE|GROUP|ORDER|HAVING|UNION|EXCEPT|INTERSECT|JOIN|INNER|LEFT|RIGHT|FULL|CROSS|OUTER|OPTION|FOR|WINDOW|LIMIT|OFFSET|SELECT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex NoAlias = new Regex(@"^((ON|JOIN|INNER|OUTER|LEFT|RIGHT|FULL|CROSS|APPLY|WHERE|WITH|FOR|TABLESAMPLE|PIVOT|UNPIVOT|GROUP|ORDER|HAVING|UNION|EXCEPT|INTERSECT|OPTION|LIMIT|OFFSET)$|\()", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex TableReferences = new Regex(@"(\bFROM\b|\bJOIN\b)\s+(?<table>\[?[\p{L}_#@][\p{L}\p{N}_#$@.\[\]]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
		public static readonly Regex CommonTableExpression = new Regex(@"(\bWITH\b|,)\s*\[?(?<name>[\p{L}_][\p{L}\p{N}_]*)\]?\s*(\([^()]*\)\s*)?\bAS\b\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
	}
}