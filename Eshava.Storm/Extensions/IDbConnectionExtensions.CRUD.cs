using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Eshava.Storm.Engines;
using Eshava.Storm.Models;

namespace Eshava.Storm
{
	public static partial class IDbConnectionExtensions
	{
		private const string DEFAULT_CRUD_COMMAND_ENGINE_NAME = "sqlconnection";
		private const string SQLITE_CRUD_COMMAND_ENGINE_NAME = "sqliteconnection";

		private static readonly string[] _wrappedConnectionPropertyNames = new[] { "WrappedConnection", "InnerConnection", "UnderlyingConnection" };

		private static readonly Dictionary<string, Func<Interfaces.IObjectGenerator, Interfaces.ICRUDCommandEngine>> _commandEngines = new Dictionary<string, Func<Interfaces.IObjectGenerator, Interfaces.ICRUDCommandEngine>>
		{
			{ DEFAULT_CRUD_COMMAND_ENGINE_NAME, objectGenerator => new SqlServerCRUDCommandEngine(objectGenerator) },
			{ SQLITE_CRUD_COMMAND_ENGINE_NAME, objectGenerator => new SqliteCRUDCommandEngine(objectGenerator) }
		};

		public static Task<K> InsertAsync<T, K>(this IDbConnection connection, T entityToInsert, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				entityToInsert,
				transaction,
				commandTimeout,
				cancellationToken);


			connection.GetCRUDCommandEngine(null).ProcessInsertRequest(commandDefinition);

			return new SqlEngine().ExecuteScalarAsync<K>(commandDefinition);
		}

		public static async Task<bool> UpdateAsync<T>(this IDbConnection connection, T entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				entityToUpdate,
				transaction,
				commandTimeout,
				cancellationToken);

			connection.GetCRUDCommandEngine(null).ProcessUpdateRequest(commandDefinition);

			var result = await new SqlEngine().ExecuteAsync(commandDefinition).ConfigureAwait(false);

			// More than one row when the key is a list, or when a trigger changes rows as well
			return result > 0;
		}

		public static async Task<bool> UpdatePartialAsync<T>(this IDbConnection connection, object entityToUpdate, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				null,
				transaction,
				commandTimeout,
				cancellationToken);

			var objectMapper = new ObjectMapper(null, null);
			connection.GetCRUDCommandEngine(objectMapper).ProcessUpdateRequest(commandDefinition, partialEntity: entityToUpdate);

			var result = await new SqlEngine().ExecuteAsync(commandDefinition).ConfigureAwait(false);

			// More than one row when the key is a list, or when a trigger changes rows as well
			return result > 0;
		}

		public static async Task<bool> UpdatePatchAsync<T>(this IDbConnection connection, IEnumerable<KeyValuePair<string, object>> propertiesToUpdate, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				null,
				transaction,
				commandTimeout,
				cancellationToken);

			var objectMapper = new ObjectMapper(null, null);
			connection.GetCRUDCommandEngine(objectMapper).ProcessUpdateRequest(commandDefinition, patchProperties: propertiesToUpdate);

			var result = await new SqlEngine().ExecuteAsync(commandDefinition).ConfigureAwait(false);

			// More than one row when the key is a list, or when a trigger changes rows as well
			return result > 0;
		}

		public static async Task<bool> DeleteAsync<T>(this IDbConnection connection, T entityToDelete, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				entityToDelete,
				transaction,
				commandTimeout,
				cancellationToken);

			connection.GetCRUDCommandEngine(null).ProcessDeleteRequest(commandDefinition);

			var result = await new SqlEngine().ExecuteAsync(commandDefinition).ConfigureAwait(false);

			// More than one row when the key is a list, or when a trigger changes rows as well
			return result > 0;
		}

		public static Task<T> QueryEntityAsync<T>(this IDbConnection connection, object id, IDbTransaction transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default) where T : class
		{
			var commandDefinition = new CommandDefinition<T>(
				connection,
				null,
				transaction,
				commandTimeout,
				cancellationToken);

			connection.GetCRUDCommandEngine(null).ProcessQueryEntityRequest(commandDefinition, id);

			return new SqlEngine().QueryFirstOrDefaultAsync<T>(commandDefinition, null);
		}

		private static Interfaces.ICRUDCommandEngine GetCRUDCommandEngine(this IDbConnection connection, Interfaces.IObjectGenerator objectGenerator)
		{
			var engineName = IsSqliteConnection(connection) ? SQLITE_CRUD_COMMAND_ENGINE_NAME : DEFAULT_CRUD_COMMAND_ENGINE_NAME;

			return _commandEngines[engineName](objectGenerator);
		}

		/// <summary>
		/// Recognises SQLite by the type name of the connection or of the connection it wraps, as profilers do.
		/// Every other connection is treated as SQL Server.
		/// </summary>
		private static bool IsSqliteConnection(IDbConnection connection)
		{
			for (var depth = 0; connection != null && depth < 5; depth++)
			{
				var type = connection.GetType();
				if (type.Name.IndexOf("sqlite", StringComparison.OrdinalIgnoreCase) >= 0
					|| (type.Namespace?.IndexOf("sqlite", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
				{
					return true;
				}

				connection = _wrappedConnectionPropertyNames
					.Select(name => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public))
					.Where(property => property != null && typeof(IDbConnection).IsAssignableFrom(property.PropertyType))
					.Select(property => property.GetValue(connection) as IDbConnection)
					.FirstOrDefault(wrapped => wrapped != null && !ReferenceEquals(wrapped, connection));
			}

			return false;
		}
	}
}