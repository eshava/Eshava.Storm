using System;

namespace Eshava.Storm.Models
{
	/// <summary>
	/// A column of a bulk insert
	/// </summary>
	public sealed class BulkInsertColumn
	{
		internal BulkInsertColumn(string name, string quotedName, Type type)
		{
			Name = name;
			QuotedName = quotedName;
			Type = type;
		}

		/// <summary>
		/// The column name without quotes, with the prefix of an owned object: <c>Home_Street</c>
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The column name quoted in the current dialect
		/// </summary>
		public string QuotedName { get; }

		/// <summary>
		/// The type of the property the column is read from, without Nullable
		/// </summary>
		public Type Type { get; }
	}
}