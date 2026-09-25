using System;
using System.Data;
using Eshava.Storm.Extensions;

namespace Eshava.Storm.Dialects
{
	/// <summary>
	/// Names are written in lower case and quoted: PostgreSQL folds a name without quotes to lower case,
	/// so this matches tables created without quotes as well as hand-written SQL that does not quote,
	/// and still protects a name that is a reserved word
	/// </summary>
	internal class PostgreSqlDialect : ISqlDialect
	{
		public string DefaultSchema => "public";

		// Npgsql chooses by the kind of the value: timestamp with time zone for UTC, timestamp without for the others
		public DbType? DateTimeDbType => null;

		/// <summary>
		/// timestamp with time zone stores an instant, not an offset, and Npgsql refuses an offset other than zero:
		/// the same instant is passed in UTC
		/// </summary>
		public object ConvertParameterValue(object value)
		{
			return value is DateTimeOffset dateTimeOffset
				? dateTimeOffset.ToUniversalTime()
				: value;
		}

		public string QuoteIdentifier(string name)
		{
			return $"\"{name.ToLowerInvariant().Replace("\"", "\"\"")}\"";
		}

		public string QuoteTableName(string schema, string table)
		{
			return schema.IsNullOrEmpty()
				? QuoteIdentifier(table)
				: $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";
		}
	}
}