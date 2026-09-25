using System.Linq.Expressions;

namespace Eshava.Storm.Linq.Models
{
	internal class WhereQueryData : WhereQuerySettings
	{
		public int Index { get; set; }

		/// <summary>
		/// The parameter of the lambda inside Any, stood in for by a placeholder that each item replaces
		/// </summary>
		public ParameterExpression InnerParameter { get; set; }
	}
}