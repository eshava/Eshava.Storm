using System.Data;
using Eshava.Storm.Extensions;

namespace Eshava.Storm.Dialects
{
	internal class SqlServerDialect : ISqlDialect
	{
		public virtual string DefaultSchema => "dbo";

		public DbType? DateTimeDbType => Settings.EnableDateTimeHighAccuracy ? DbType.DateTime2 : DbType.DateTime;

		public object ConvertParameterValue(object value)
		{
			return value;
		}

		public string QuoteIdentifier(string name)
		{
			return $"[{name.Replace("]", "]]")}]";
		}

		public string QuoteTableName(string schema, string table)
		{
			return schema.IsNullOrEmpty()
				? QuoteIdentifier(table)
				: $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";
		}
	}
}