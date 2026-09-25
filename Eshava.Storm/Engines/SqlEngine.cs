using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Interfaces;
using Eshava.Storm.Models;

namespace Eshava.Storm.Engines
{
	internal class SqlEngine
	{
		public Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition commandDefinition, Func<IObjectMapper, T> map)
		{
			return QueryThingsAsync(commandDefinition, map, false);
		}

		public async Task<T> QueryFirstOrDefaultAsync<T>(CommandDefinition commandDefinition, Func<IObjectMapper, T> map)
		{
			return (await QueryThingsAsync(commandDefinition, map, true).ConfigureAwait(false)).FirstOrDefault();
		}

		public Task<T> ExecuteScalarAsync<T>(CommandDefinition commandDefinition)
		{
			return ExecuteSomethingAsync<T>(commandDefinition);
		}

		public Task<int> ExecuteAsync(CommandDefinition commandDefinition)
		{
			return ExecuteSomethingAsync(commandDefinition);
		}

		private async Task<IEnumerable<T>> QueryThingsAsync<T>(CommandDefinition commandDefinition, Func<IObjectMapper, T> map, bool firstRowOnly)
		{
			var parameterReader = GetParameterReader(commandDefinition.Parameters);
			var wasClosed = commandDefinition.Connection.State == ConnectionState.Closed;

			using (var command = (DbCommand)SetupCommand(commandDefinition, parameterReader))
			{
				var reader = default(DbDataReader);
				try
				{
					if (wasClosed)
					{
						await ((DbConnection)commandDefinition.Connection).OpenAsync(commandDefinition.CancellationToken).ConfigureAwait(false);
					}

					var behavior = CommandBehavior.SingleResult | CommandBehavior.KeyInfo;
					if (firstRowOnly)
					{
						behavior |= CommandBehavior.SingleRow;
					}

					reader = await command.ExecuteReaderAsync(GetBehavior(wasClosed, behavior), commandDefinition.CancellationToken).ConfigureAwait(false);

					var objectMapper = new ObjectMapper(reader, commandDefinition.CommandText);
					var buffer = new List<T>();

					while (await reader.ReadAsync(commandDefinition.CancellationToken).ConfigureAwait(false))
					{
						buffer.Add(Deserialize(objectMapper, map));

						if (firstRowOnly)
						{
							break;
						}
					}

					while (await reader.NextResultAsync(commandDefinition.CancellationToken).ConfigureAwait(false))
					{ }

					return buffer;
				}
				finally
				{
					using (reader)
					{
						// dispose if non-null
					}

					if (wasClosed)
					{
						commandDefinition.Connection.Close();
					}
				}
			}
		}

		private async Task<int> ExecuteSomethingAsync(CommandDefinition commandDefinition)
		{
			var parameterReader = GetParameterReader(commandDefinition.Parameters);
			var wasClosed = commandDefinition.Connection.State == ConnectionState.Closed;

			using (var command = (DbCommand)SetupCommand(commandDefinition, parameterReader))
			{
				try
				{
					if (wasClosed)
					{
						await ((DbConnection)commandDefinition.Connection).OpenAsync(commandDefinition.CancellationToken).ConfigureAwait(false);
					}

					return await command.ExecuteNonQueryAsync(commandDefinition.CancellationToken).ConfigureAwait(false);
				}
				finally
				{
					if (wasClosed)
					{
						commandDefinition.Connection.Close();
					}
				}
			}
		}

		private async Task<T> ExecuteSomethingAsync<T>(CommandDefinition commandDefinition)
		{
			var parameterReader = GetParameterReader(commandDefinition.Parameters);
			var wasClosed = commandDefinition.Connection.State == ConnectionState.Closed;

			using (var command = (DbCommand)SetupCommand(commandDefinition, parameterReader))
			{
				try
				{
					if (wasClosed)
					{
						await ((DbConnection)commandDefinition.Connection).OpenAsync(commandDefinition.CancellationToken).ConfigureAwait(false);
					}

					var result = await command.ExecuteScalarAsync(commandDefinition.CancellationToken).ConfigureAwait(false);

					return new DataTypeMapper().Map<T>(result);
				}
				finally
				{
					if (wasClosed)
					{
						commandDefinition.Connection.Close();
					}
				}
			}
		}

		private T Deserialize<T>(IObjectMapper objectMapper, Func<IObjectMapper, T> map)
		{
			if (map == null)
			{
				return objectMapper.Map<T>(null);
			}

			return map(objectMapper);
		}

		private static IEnumerable GetMultiExec(object param)
		{
			return (
					param is IEnumerable
					&& !(
							param is string
							|| param is IEnumerable<KeyValuePair<string, object>>
							|| param is IDictionary
						)
					)
				? (IEnumerable)param
				: null;
		}

		private static Action<IDbCommand, object> GetParameterReader(object exampleParameters)
		{
			if (GetMultiExec(exampleParameters) != null)
			{
				throw new InvalidOperationException("An enumerable sequence of parameters (arrays, lists, etc) is not allowed in this context");
			}

			return (cmd, obj) =>
			{
				var mapped = new ParameterCollector(obj);
				mapped.AddParameters(cmd);
			};
		}

		private IDbCommand SetupCommand(CommandDefinition commandDefinition, Action<IDbCommand, object> paramReader)
		{
			var command = commandDefinition.Connection.CreateCommand();
			command.CommandText = commandDefinition.CommandText;

			if (commandDefinition.Transaction != null)
			{
				command.Transaction = commandDefinition.Transaction;
			}

			if (commandDefinition.CommandTimeout.HasValue)
			{
				command.CommandTimeout = commandDefinition.CommandTimeout.Value;
			}
			else if (Settings.CommandTimeout.HasValue)
			{
				command.CommandTimeout = Settings.CommandTimeout.Value;
			}

			if (commandDefinition.CommandType.HasValue)
			{
				command.CommandType = commandDefinition.CommandType.Value;
			}

			paramReader?.Invoke(command, commandDefinition.Parameters);

			return command;
		}

		private static CommandBehavior GetBehavior(bool close, CommandBehavior @default)
		{
			return close ? (@default | CommandBehavior.CloseConnection) : @default;
		}
	}
}