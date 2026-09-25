using System;
using System.Collections.Generic;
using System.Data;
using Eshava.Storm.Engines;

namespace Eshava.Storm.Models
{
	/// <summary>
	/// What a bulk insert writes, independent of the provider that writes it: the table, its columns and a row of
	/// values per entity. The values are the ones an insert would pass as parameters — type handlers applied,
	/// enums as their underlying type, owned objects as their columns, and the columns of a generated key left out.
	/// </summary>
	public sealed class BulkInsertRows
	{
		internal BulkInsertRows(string tableName, IReadOnlyList<BulkInsertColumn> columns, IEnumerable<object[]> rows)
		{
			TableName = tableName;
			Columns = columns;
			Rows = rows;
		}

		/// <summary>
		/// The table, quoted in the current dialect, or the custom name as it was given
		/// </summary>
		public string TableName { get; }

		public IReadOnlyList<BulkInsertColumn> Columns { get; }

		/// <summary>
		/// One array per entity, in the order of <see cref="Columns"/>; null for a missing value. Read lazily.
		/// </summary>
		public IEnumerable<object[]> Rows { get; }

		/// <summary>
		/// Prepares the rows of a bulk insert
		/// </summary>
		/// <param name="entities">The entities to insert, at least one</param>
		/// <param name="createParameter">Creates a parameter of the provider, for type handlers to set a value on</param>
		/// <param name="customTableName">A table name to use instead of the configured one, taken as it is</param>
		public static BulkInsertRows Create<T>(IEnumerable<T> entities, Func<IDbDataParameter> createParameter, string customTableName = null) where T : class
		{
			return new BulkInsertRowsEngine().Create(entities, createParameter, customTableName);
		}
	}
}