using System.Data;
using Eshava.Storm.Extensions;
using Microsoft.Data.SqlClient;

namespace Eshava.Storm.QueryParameters
{
	internal static class TableParameter
	{
		internal static void Set(IDbDataParameter parameter, DataTable table, string typeName)
		{
			parameter.Value = table.SanitizeParameterValue();

			if (typeName.IsNullOrEmpty() && table != null)
			{
				typeName = table.GetTypeName();
			}

			if (!typeName.IsNullOrEmpty() && parameter is SqlParameter sqlParam)
			{
				sqlParam.TypeName = typeName;
				sqlParam.SqlDbType = SqlDbType.Structured;
			}
		}
	}
}