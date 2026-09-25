using System.Data;

namespace Eshava.Storm.Dialects
{
	internal interface ISqlDialect
	{
		/// <summary>
		/// The schema of a table the statement names without one
		/// </summary>
		string DefaultSchema { get; }

		/// <summary>
		/// The type a DateTime parameter is sent with, or null to leave it to the provider
		/// </summary>
		DbType? DateTimeDbType { get; }

		/// <summary>
		/// Adapts a parameter value to what the database stores; the value itself is not changed
		/// </summary>
		object ConvertParameterValue(object value);

		string QuoteIdentifier(string name);

		string QuoteTableName(string schema, string table);
	}
}