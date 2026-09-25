using System;
using System.Collections.Generic;
using System.Data;

namespace Eshava.Storm.Models
{
	internal static class DbTypeMap
	{
		// Built once; the map is only ever read
		internal static Dictionary<Type, DbType> Map { get; } = new Dictionary<Type, DbType>
		{
			[typeof(byte)] = DbType.Byte,
			[typeof(sbyte)] = DbType.SByte,
			[typeof(short)] = DbType.Int16,
			[typeof(ushort)] = DbType.UInt16,
			[typeof(int)] = DbType.Int32,
			[typeof(uint)] = DbType.UInt32,
			[typeof(long)] = DbType.Int64,
			[typeof(ulong)] = DbType.UInt64,
			[typeof(float)] = DbType.Single,
			[typeof(double)] = DbType.Double,
			[typeof(decimal)] = DbType.Decimal,
			[typeof(bool)] = DbType.Boolean,
			[typeof(string)] = DbType.String,
			[typeof(char)] = DbType.StringFixedLength,
			[typeof(Guid)] = DbType.Guid,
			[typeof(DateTimeOffset)] = DbType.DateTimeOffset,
			[typeof(TimeSpan)] = DbType.Time,
			[typeof(byte[])] = DbType.Binary,
			[typeof(byte?)] = DbType.Byte,
			[typeof(sbyte?)] = DbType.SByte,
			[typeof(short?)] = DbType.Int16,
			[typeof(ushort?)] = DbType.UInt16,
			[typeof(int?)] = DbType.Int32,
			[typeof(uint?)] = DbType.UInt32,
			[typeof(long?)] = DbType.Int64,
			[typeof(ulong?)] = DbType.UInt64,
			[typeof(float?)] = DbType.Single,
			[typeof(double?)] = DbType.Double,
			[typeof(decimal?)] = DbType.Decimal,
			[typeof(bool?)] = DbType.Boolean,
			[typeof(char?)] = DbType.StringFixedLength,
			[typeof(Guid?)] = DbType.Guid,
			[typeof(DateTimeOffset?)] = DbType.DateTimeOffset,
			[typeof(TimeSpan?)] = DbType.Time,
#if NET6_0_OR_GREATER
			[typeof(DateOnly)] = DbType.Date,
			[typeof(TimeOnly)] = DbType.Time,
			[typeof(DateOnly?)] = DbType.Date,
			[typeof(TimeOnly?)] = DbType.Time,
#endif
			[typeof(object)] = DbType.Object
		};
	}
}