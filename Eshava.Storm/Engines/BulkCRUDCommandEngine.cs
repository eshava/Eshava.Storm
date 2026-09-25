using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;
using Eshava.Storm.Extensions;
using Eshava.Storm.Interfaces;
using Eshava.Storm.Models;
using Microsoft.Data.SqlClient;

namespace Eshava.Storm.Engines
{
	internal class BulkCRUDCommandEngine : AbstractCRUDCommandEngine
	{
		private static Type _ibulkInsertTypeHandler = typeof(IBulkInsertTypeHandler);

		public BulkCRUDCommandEngine() : base(null) { }

		public async Task BulkInsertAsync<T>(BulkCommandDefinition<T> commandDefinition) where T : class
		{
			var type = CheckCommandConditions(commandDefinition.Entities, "insert");
			var entityTypeResult = MetaData.TypeAnalyzer.GetOrAnalyzeEntity(type);

			if (!entityTypeResult.HasPrimaryKey())
			{
				throw new ArgumentException("At least one key column property must be defined.");
			}

			if (commandDefinition.Connection.State == ConnectionState.Closed)
			{
				await commandDefinition.Connection.OpenAsync(commandDefinition.CancellationToken).ConfigureAwait(false);
			}

			var tableName = entityTypeResult.TableName;
			if (!commandDefinition.TableName.IsNullOrEmpty())
			{
				tableName = commandDefinition.TableName;
			}

			var sqlBulkCopy = commandDefinition.Transaction == default
				? new SqlBulkCopy(commandDefinition.Connection)
				{
					DestinationTableName = tableName
				}
				: new SqlBulkCopy(commandDefinition.Connection, SqlBulkCopyOptions.Default, commandDefinition.Transaction)
				{
					DestinationTableName = tableName
				};

			if (commandDefinition.CommandTimeout.HasValue)
			{
				sqlBulkCopy.BulkCopyTimeout = commandDefinition.CommandTimeout.Value;
			}

			var properties = GetProperties(new PropertyRequest
			{
				Type = type,
				Entity = commandDefinition.Entities.First()
			});

			var dataTable = CreateDataTable(properties, sqlBulkCopy);

			foreach (var entity in commandDefinition.Entities)
			{
				// Must be executed for all entities, because OwnsOne Properties can be filled or not filled
				properties = GetProperties(new PropertyRequest
				{
					Type = type,
					Entity = entity
				});

				var row = dataTable.NewRow();

				foreach (var property in properties)
				{
					var columnName = property.Prefix.IsNullOrEmpty()
						? property.ColumnName
						: property.Prefix + property.ColumnName
						;

					if (property.TypeHandler == default)
					{
						row[columnName] = property.PropertyInfo.GetValue(property.Entity) ?? DBNull.Value;

						continue;
					}

					var cellValue = property.PropertyInfo.GetValue(property.Entity);
					if (cellValue == null)
					{
						row[columnName] = DBNull.Value;
					}
					else
					{
						var sqlParameter = new SqlParameter();
						property.TypeHandler.SetValue(sqlParameter, cellValue);
						row[columnName] = sqlParameter.Value;
					}
				}

				dataTable.Rows.Add(row);
			}

			await sqlBulkCopy.WriteToServerAsync(dataTable, commandDefinition.CancellationToken);
		}

		private DataTable CreateDataTable(IEnumerable<Property> properties, SqlBulkCopy sqlBulkCopy)
		{
			var dataTable = new DataTable();
			foreach (var property in properties)
			{
				var columnName = property.Prefix.IsNullOrEmpty()
					? property.ColumnName
					: property.Prefix + property.ColumnName
					;

				sqlBulkCopy.ColumnMappings.Add(columnName, columnName);

				if (property.TypeHandler == default || !property.TypeHandler.ReadAsByteArray)
				{
					var propertyType = property.PropertyInfo.PropertyType.GetDataType();
					if (property.TypeHandler?.GetType().ImplementsInterface(_ibulkInsertTypeHandler) ?? false)
					{
						propertyType = ((IBulkInsertTypeHandler)property.TypeHandler).GetDateType();
					}
					else if (property.TypeHandler != default)
					{
						// The handler decides the value, and with it its type
						propertyType = typeof(object);
					}

					dataTable.Columns.Add(new DataColumn(columnName, propertyType));

					continue;
				}

				dataTable.Columns.Add(new DataColumn(columnName, typeof(SqlBytes)));
			}

			return dataTable;
		}

		private Type CheckCommandConditions<T>(IEnumerable<T> entities, string action, IEnumerable<object> partialEntity = null) where T : class
		{
			var type = typeof(T);

			if (type.IsArray || type.ImplementsIEnumerable())
			{
				throw new ArgumentException($"Entity to {action} must be a single instance. No enumeration or array.");
			}

			if ((entities == default || !entities.Any()) && (partialEntity == default || !partialEntity.Any()))
			{
				throw new ArgumentNullException($"Entity to {action} must not be NULL");
			}

			return type;
		}

		protected override string GetLastInsertedPrimaryKeyQuery() => throw new NotImplementedException();
	}
}