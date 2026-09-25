using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

namespace Eshava.Storm.QueryParameters
{
	internal static class SqlDataRecordParameter
	{
		internal static void Set(IDbDataParameter parameter, IEnumerable<SqlDataRecord> data, string typeName)
		{
			parameter.Value = (object)data ?? DBNull.Value;

			var sqlParam = parameter as SqlParameter;
			if (sqlParam != null)
			{
				sqlParam.SqlDbType = SqlDbType.Structured;
				sqlParam.TypeName = typeName;
			}
		}
	}
}