using System;

namespace Eshava.Storm.Models
{
	internal class ColumnCacheItem
	{
		public ColumnCacheItem(int ordinal, Type dataType, string columnName, string tableName, string schemaName)
		{
			Ordinal = ordinal;
			DataType = dataType;
			ColumnName = columnName;
			TableName = tableName;
			SchemaName = schemaName;
		}

		public string ColumnName { get; set; }
		public string TableName { get; set; }
		public string SchemaName { get; set; }

		public int Ordinal { get; set; }
		public Type DataType { get; set; }
		public string TableAlias { get; set; }
	}
}