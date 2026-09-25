#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Linq;

namespace Eshava.Storm.Handler
{
	/// <summary>
	/// Builds a SQL statement from an interpolated string.
	/// A hole directly preceded by <c>@</c> becomes a parameter: <c>WHERE Name = @{name}</c>.
	/// Any other hole is inserted into the statement as text, which is meant for identifiers such as
	/// table and column names: <c>FROM {TypeAnalyzer.GetTableName&lt;Alpha&gt;()}</c>.
	/// Never put a value into such a hole — it is not escaped, so a value that comes from outside the
	/// application opens the statement to SQL injection.
	/// </summary>
	[InterpolatedStringHandler]
	public ref struct SqlInterpolatedStringHandler
	{
		private DefaultInterpolatedStringHandler _innerHandler;
		private Dictionary<string, object> _parameters;
		private string _prefix;
		private bool _hasAtSymbol;

		public readonly IReadOnlyDictionary<string, object> Parameters => new ReadOnlyDictionary<string, object>(_parameters);

		public SqlInterpolatedStringHandler(int literalLength, int formattedCount)
		{
			_parameters = new Dictionary<string, object>(formattedCount);
			_innerHandler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
			_prefix = Guid.NewGuid().ToString()[..4];
		}

		public void AppendLiteral(string literal)
		{
			_innerHandler.AppendLiteral(literal);
			_hasAtSymbol = literal.Last() == '@';
		}

		/// <summary>
		/// Adds a parameter when the preceding literal ends with <c>@</c>, and the value as unescaped text otherwise
		/// </summary>
		public void AppendFormatted<T>(T value)
		{
			if (_hasAtSymbol)
			{
				var paramName = _prefix + "_" + (_parameters.Count + 1).ToString();

				_parameters.Add(paramName, value);
				_innerHandler.AppendLiteral(paramName);
				_hasAtSymbol = false;
			}
			else
			{
				_innerHandler.AppendLiteral(value?.ToString());
			}
		}

		public string ToStringAndClear() => _innerHandler.ToStringAndClear();
		public override string ToString() => _innerHandler.ToString();
	}
}

#endif