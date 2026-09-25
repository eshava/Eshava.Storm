using System.Linq.Expressions;

namespace Eshava.Storm.Linq.Visitors
{
	/// <summary>
	/// Counts how often a parameter is used, and how often directly as an operand of a comparison
	/// </summary>
	internal sealed class ParameterUsageCounter : ExpressionVisitor
	{
		private readonly ParameterExpression _parameter;
		private int _uses;
		private int _comparisonOperandUses;

		private ParameterUsageCounter(ParameterExpression parameter)
		{
			_parameter = parameter;
		}

		/// <summary>
		/// True if the parameter is only ever compared as it is, <c>v =&gt; v == x.Id</c>, never accessed, <c>v =&gt; v.Id == x.Id</c>
		/// </summary>
		public static bool IsOnlyComparedDirectly(Expression expression, ParameterExpression parameter)
		{
			var counter = new ParameterUsageCounter(parameter);
			counter.Visit(expression);

			return counter._uses == counter._comparisonOperandUses;
		}

		protected override Expression VisitBinary(BinaryExpression node)
		{
			if (IsComparison(node.NodeType))
			{
				if (Unwrap(node.Left) == _parameter)
				{
					_comparisonOperandUses++;
				}

				if (Unwrap(node.Right) == _parameter)
				{
					_comparisonOperandUses++;
				}
			}

			return base.VisitBinary(node);
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			if (node == _parameter)
			{
				_uses++;
			}

			return node;
		}

		private static Expression Unwrap(Expression expression)
		{
			while (expression.NodeType == ExpressionType.Convert)
			{
				expression = ((UnaryExpression)expression).Operand;
			}

			return expression;
		}

		private static bool IsComparison(ExpressionType expressionType)
		{
			return expressionType == ExpressionType.Equal
				|| expressionType == ExpressionType.NotEqual
				|| expressionType == ExpressionType.GreaterThan
				|| expressionType == ExpressionType.GreaterThanOrEqual
				|| expressionType == ExpressionType.LessThan
				|| expressionType == ExpressionType.LessThanOrEqual;
		}
	}
}