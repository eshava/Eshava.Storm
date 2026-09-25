using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using Eshava.Storm.Handler;
using Eshava.Storm.Interfaces;
using Microsoft.Data.SqlClient.Server;

namespace Eshava.Storm.Models
{
	internal static class TypeHandlerMap
	{
		private static readonly Type _typeOfDataTable = typeof(DataTable);
		private static readonly Type _typeOfSqlDataRecords = typeof(IEnumerable<SqlDataRecord>);

		// Filled by the static initializer, so the built-in handlers are present before the first registration
		internal static ConcurrentDictionary<Type, ITypeHandler> Map { get; } = CreateMap();

		public static void Add(Type type, ITypeHandler typeHandler)
		{
			Map.TryAdd(type, typeHandler);
		}

		/// <summary>
		/// Finds the handler for a parameter value type. Besides the exact type, the built-in handlers also
		/// cover derived data tables and every implementation of an enumeration of data records.
		/// </summary>
		public static bool TryGetHandler(Type type, out ITypeHandler handler)
		{
			if (Map.TryGetValue(type, out handler))
			{
				return true;
			}

			if (_typeOfDataTable.IsAssignableFrom(type))
			{
				return Map.TryGetValue(_typeOfDataTable, out handler);
			}

			if (_typeOfSqlDataRecords.IsAssignableFrom(type))
			{
				return Map.TryGetValue(_typeOfSqlDataRecords, out handler);
			}

			return false;
		}

		private static ConcurrentDictionary<Type, ITypeHandler> CreateMap()
		{
			var map = new ConcurrentDictionary<Type, ITypeHandler>();
			map.TryAdd(_typeOfDataTable, new DataTableHandler());
			map.TryAdd(_typeOfSqlDataRecords, new SqlDataRecordHandler());

			return map;
		}
	}
}