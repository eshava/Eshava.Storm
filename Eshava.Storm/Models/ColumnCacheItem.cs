using System;

namespace Eshava.Storm.Models
{
	internal class ColumnCacheItem
	{
		public ColumnCacheItem(int ordinal, Type dataType, string columnName, string tableName, string schemaName, string baseColumnName = null)
		{
			Ordinal = ordinal;
			DataType = dataType;
			ColumnName = columnName;
			TableName = tableName;
			SchemaName = schemaName;
			BaseColumnName = baseColumnName ?? columnName;
		}

		public string ColumnName { get; set; }
		public string TableName { get; set; }
		public string SchemaName { get; set; }

		/// <summary>
		/// The column name in its table, which differs from the column name for a column renamed with AS
		/// </summary>
		public string BaseColumnName { get; set; }

		public int Ordinal { get; set; }
		public Type DataType { get; set; }
		public string TableAlias { get; set; }
	}
}