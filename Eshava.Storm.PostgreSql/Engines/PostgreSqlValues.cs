using System;

namespace Eshava.Storm.PostgreSql.Engines
{
	/// <summary>
	/// Adapts a value to the type of its column, as the server would when it casts a parameter of an insert;
	/// binary COPY leaves that to the client, and a value Npgsql refuses ends the connection
	/// </summary>
	internal static class PostgreSqlValues
	{
		private const string TIMESTAMPWITHTIMEZONE = "timestamp with time zone";
		private const string TIMESTAMPWITHOUTTIMEZONE = "timestamp without time zone";

		public static object ForColumn(object value, string dataTypeName)
		{
			switch (value)
			{
				case DateTime dateTime when dataTypeName == TIMESTAMPWITHTIMEZONE:
					// An instant: a value without kind is taken as UTC, as the server does with the session time zone UTC
					return dateTime.Kind switch
					{
						DateTimeKind.Utc => dateTime,
						DateTimeKind.Local => dateTime.ToUniversalTime(),
						_ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
					};
				case DateTimeOffset dateTimeOffset when dataTypeName == TIMESTAMPWITHTIMEZONE:
					return dateTimeOffset.ToUniversalTime();
				case DateTime dateTime when dataTypeName == TIMESTAMPWITHOUTTIMEZONE:
					// A wall clock time: the value as it reads, without a kind
					return dateTime.Kind == DateTimeKind.Unspecified ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
				case DateTimeOffset dateTimeOffset when dataTypeName == TIMESTAMPWITHOUTTIMEZONE:
					return DateTime.SpecifyKind(dateTimeOffset.UtcDateTime, DateTimeKind.Unspecified);
			}

			return value;
		}
	}
}