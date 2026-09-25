using System.Linq.Expressions;

namespace Eshava.Storm.Linq.Visitors
{
	internal sealed class ParameterReplacer : ExpressionVisitor
	{
		private readonly ParameterExpression _parameter;
		private readonly Expression _replacement;

		private ParameterReplacer(ParameterExpression parameter, Expression replacement)
		{
			_parameter = parameter;
			_replacement = replacement;
		}

		public static Expression Replace(Expression expression, ParameterExpression parameter, Expression replacement)
		{
			return new ParameterReplacer(parameter, replacement).Visit(expression);
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			return node == _parameter ? _replacement : node;
		}
	}
}