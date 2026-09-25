using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Eshava.Storm.Dialects;
using Eshava.Storm.Extensions;
using Eshava.Storm.Models;

namespace Eshava.Storm.Engines
{
	/// <summary>
	/// Collects columns and values for a bulk insert through the same property analysis the insert uses
	/// </summary>
	internal class BulkInsertRowsEngine : AbstractCRUDCommandEngine
	{
		public BulkInsertRowsEngine() : base(null) { }

		public BulkInsertRows Create<T>(IEnumerable<T> entities, Func<IDbDataParameter> createParameter, string customTableName) where T : class
		{
			if (entities == null || !entities.Any())
			{
				throw new ArgumentNullException(nameof(entities), "Entities to insert must not be empty");
			}

			if (createParameter == null)
			{
				throw new ArgumentNullException(nameof(createParameter));
			}

			var type = typeof(T);
			if (type.IsArray || type.ImplementsIEnumerable())
			{
				throw new ArgumentException("Entity to insert must be a single instance. No enumeration or array.");
			}

			var entityTypeResult = MetaData.TypeAnalyzer.GetOrAnalyzeEntity(type);
			if (!entityTypeResult.HasPrimaryKey())
			{
				throw new ArgumentException("At least one key column property must be defined.");
			}

			// Every entity has the same columns: an owned object that is not set contributes its columns as NULL
			var columns = GetProperties(new PropertyRequest { Type = type, Entity = entities.First() })
				.Select(property =>
				{
					var name = property.Prefix + property.ColumnName;

					return new BulkInsertColumn(name, SqlDialects.Current.QuoteIdentifier(name), property.PropertyInfo.PropertyType.GetDataType());
				})
				.ToList();

			var tableName = customTableName.IsNullOrEmpty() ? entityTypeResult.TableName : customTableName;

			return new BulkInsertRows(tableName, columns, ReadRows(type, entities, columns, createParameter));
		}

		private IEnumerable<object[]> ReadRows<T>(Type type, IEnumerable<T> entities, IReadOnlyList<BulkInsertColumn> columns, Func<IDbDataParameter> createParameter)
		{
			var ordinals = columns
				.Select((column, index) => (column.Name, Index: index))
				.ToDictionary(column => column.Name, column => column.Index);

			foreach (var entity in entities)
			{
				var row = new object[columns.Count];

				foreach (var property in GetProperties(new PropertyRequest { Type = type, Entity = entity }))
				{
					if (ordinals.TryGetValue(property.Prefix + property.ColumnName, out var ordinal))
					{
						row[ordinal] = ReadValue(property, createParameter);
					}
				}

				yield return row;
			}
		}

		private static object ReadValue(Models.Property property, Func<IDbDataParameter> createParameter)
		{
			var value = property.GetValue();
			if (value == null)
			{
				return null;
			}

			if (property.TypeHandler == null)
			{
				return SqlDialects.Current.ConvertParameterValue(value.SanitizeParameterValue());
			}

			var parameter = createParameter();
			property.TypeHandler.SetValue(parameter, value);

			return parameter.Value == DBNull.Value ? null : parameter.Value;
		}

		protected override string GetLastInsertedPrimaryKeyQuery() => throw new NotSupportedException();
	}
}