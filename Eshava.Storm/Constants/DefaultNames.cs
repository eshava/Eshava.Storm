namespace Eshava.Storm.Constants
{
	internal static class DefaultNames
	{
		/// <summary>
		/// Key used to indicate the type name associated with a DataTable
		/// </summary>
		public const string DATATABLETYPENAMEKEY = "eshava.storm:TypeName";

		public const string LINQBINARY = "System.Data.Linq.Binary";

		// Referenced by name, the netstandard2.0 target does not know these types
		public const string DATEONLY = "System.DateOnly";
		public const string TIMEONLY = "System.TimeOnly";
	}
}