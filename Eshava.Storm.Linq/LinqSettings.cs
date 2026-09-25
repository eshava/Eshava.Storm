using Eshava.Storm.Linq.Enums;

namespace Eshava.Storm.Linq
{
	/// <summary>
	/// Global settings of the translation. Set them once at the start of the application.
	/// </summary>
	public static class LinqSettings
	{
		static LinqSettings()
		{
			SetDefaults();
		}

		/// <summary>
		/// Resets all settings to their default values
		/// </summary>
		public static void SetDefaults()
		{
			Dialect = QueryDialect.SqlServer;
			TranslateCaseConversion = null;
		}

		/// <summary>
		/// The SQL dialect the conditions are written in. It decides how the wildcards of LIKE are escaped and,
		/// unless <see cref="TranslateCaseConversion"/> says otherwise, what happens to ToLower and ToUpper.
		/// Default: SQL Server.
		/// </summary>
		public static QueryDialect Dialect { get; set; }

		/// <summary>
		/// What happens to ToLower() and ToUpper() on a column.
		/// null (default): follows the dialect — ignored on SQL Server and SQLite, where the collation usually
		/// ignores case and a function on the column would keep an index from being used; translated on
		/// PostgreSQL, which compares case-sensitively.
		/// true: always translated, to lower()/upper(), and to ILIKE for a LIKE on PostgreSQL.
		/// false: always ignored.
		/// </summary>
		public static bool? TranslateCaseConversion { get; set; }

		internal static bool IsCaseConversionTranslated => TranslateCaseConversion ?? Dialect == QueryDialect.PostgreSql;
	}
}