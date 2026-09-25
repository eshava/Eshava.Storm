using System.Reflection;
using Eshava.Storm.Interfaces;

namespace Eshava.Storm.Models
{
	internal class Property
	{
		public string Prefix { get; set; }
		public PropertyInfo PropertyInfo { get; set; }
		public object Entity { get; set; }
		public ITypeHandler TypeHandler { get; set; }
		public string ColumnName { get; set; }

		/// <summary>
		/// The value to write; a property of an owned object that is not set has no entity and writes NULL
		/// </summary>
		public object GetValue()
		{
			return Entity == null ? null : PropertyInfo.GetValue(Entity);
		}
	}
}