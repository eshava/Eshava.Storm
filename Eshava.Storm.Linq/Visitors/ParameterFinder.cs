using System.Collections.Generic;
using System.Linq.Expressions;

namespace Eshava.Storm.Linq.Visitors
{
	/// <summary>
	/// Finds out whether an expression depends on a parameter it does not declare itself.
	/// An expression that does not can be evaluated to a value before it is translated.
	/// </summary>
	internal sealed class ParameterFinder : ExpressionVisitor
	{
		private readonly HashSet<ParameterExpression> _declaredParameters = new HashSet<ParameterExpression>();
		private bool _found;

		public static bool ContainsParameter(Expression expression)
		{
			var finder = new ParameterFinder();
			finder.Visit(expression);

			return finder._found;
		}

		public override Expression Visit(Expression node)
		{
			return _found ? node : base.Visit(node);
		}

		protected override Expression VisitLambda<T>(Expression<T> node)
		{
			foreach (var parameter in node.Parameters)
			{
				_declaredParameters.Add(parameter);
			}

			Visit(node.Body);

			foreach (var parameter in node.Parameters)
			{
				_declaredParameters.Remove(parameter);
			}

			return node;
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			if (!_declaredParameters.Contains(node))
			{
				_found = true;
			}

			return node;
		}
	}
}