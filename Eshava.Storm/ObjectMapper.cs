using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Eshava.Storm.Extensions;
using Eshava.Storm.Interfaces;
using Eshava.Storm.MetaData;
using Eshava.Storm.MetaData.Models;
using Eshava.Storm.Models;

namespace Eshava.Storm
{
	internal class ObjectMapper : IObjectMapper, IObjectGenerator
	{
		private readonly DbDataReader _reader;
		private readonly DataTypeMapper _dataTypeMapper;
		private TableAnalysisResult _tableAnalysisResult;
		private DataTable _schemaTable = null;

		public ObjectMapper(DbDataReader reader, string sql)
		{
			_dataTypeMapper = new DataTypeMapper();
			_reader = reader;
			SetTableAnalysisResult(sql);
		}

		public DataTable GetSchemaTable()
		{
			CalculateColumnCache();

			return _schemaTable;
		}

		public Type GetDataType(string columnName, string tableAlias = null)
		{
			CalculateColumnCache();

			(var hasInvalidAlias, var requestedTableNames) = GetTableNamesFromAlias(tableAlias);
			if (hasInvalidAlias)
			{
				return default;
			}

			columnName = columnName?.ToLower() ?? "";

			if (!_tableAnalysisResult.ResultTableNames.Any() || !requestedTableNames.Any())
			{
				// No result analyse result available
				if (!_tableAnalysisResult.ColumnCache.ContainsKey(columnName))
				{
					var columnNew = _tableAnalysisResult.ColumnCache.Keys.FirstOrDefault(c => c.EndsWith("." + columnName));
					if (!columnNew.IsNullOrEmpty())
					{
						columnName = columnNew;
					}
				}

				if (_tableAnalysisResult.ColumnCache.ContainsKey(columnName))
				{
					return _tableAnalysisResult.ColumnCache[columnName].Last().DataType;
				}

				return default;
			}

			var columnFound = false;
			foreach (var requestedTableName in requestedTableNames)
			{
				var fullColumnNames = requestedTableName.TableNames.Select(tableName => $"{tableName}.{columnName}").ToList();
				var fullColumnName = fullColumnNames.FirstOrDefault(f => _tableAnalysisResult.ColumnCache.ContainsKey(f));

				if (fullColumnName.IsNullOrEmpty())
				{
					continue;
				}

				columnFound = true;
				if (!_tableAnalysisResult.AliasOccurrences.ContainsKey(requestedTableName.Alias))
				{
					if (requestedTableName.TableNames.Any(t => t == requestedTableName.Alias))
					{
						return _tableAnalysisResult.ColumnCache[fullColumnName].Last().DataType;
					}

					// Skip property if alias is unknown

					continue;
				}

				if (!Settings.EnableValueReadingBasedOnTableAliasOccurrence)
				{
					var column = _tableAnalysisResult.ColumnCache[fullColumnName].FirstOrDefault(c => c.TableAlias == requestedTableName.Alias);
					if (column is not null)
					{
						return column.DataType;
					}
				}

				var aliasOccurrence = _tableAnalysisResult.AliasOccurrences[requestedTableName.Alias];
				if (aliasOccurrence >= _tableAnalysisResult.ColumnCache[fullColumnName].Count)
				{
					// Skip if correct column occurrence could not be found

					continue;
				}

				return _tableAnalysisResult.ColumnCache[fullColumnName][aliasOccurrence].DataType;
			}

			if (!columnFound)
			{
				var fullColumnName = $"none.{columnName}";

				if (_tableAnalysisResult.ColumnCache.ContainsKey(fullColumnName))
				{
					return _tableAnalysisResult.ColumnCache[fullColumnName].Last().DataType;
				}
			}

			return default;
		}

		public T GetValue<T>(string columnName, string tableAlias = null)
		{
			var valueType = typeof(T).GetDataType();

			if ((ShouldMapClass<T>() && !TypeHandlerMap.Map.ContainsKey(valueType)) || columnName.IsNullOrEmpty())
			{
				return default;
			}

			CalculateColumnCache();

			(var hasInvalidAlias, var requestedTableNames) = GetTableNamesFromAlias(tableAlias);
			if (hasInvalidAlias)
			{
				return default;
			}

			var readerAccessItems = new List<ReaderAccessItem>();
			var information = new PreProcessPropertyInformation
			{
				ReaderAccessItems = readerAccessItems,
				Instance = null,
				RequestedTableNames = requestedTableNames
			};

			CollectOrdinal(null, information, columnName.ToLower());

			if (readerAccessItems.Count == 0)
			{
				return default;
			}

			return ExecuteReaderAccessItem<T>(readerAccessItems.First());
		}

		public T Map<T>(string tableAlias = null)
		{
			if (ShouldMapClass<T>())
			{
				CalculateColumnCache();

				var resultObject = Activator.CreateInstance<T>();
				(var hasInvalidAlias, var requestedTableNames) = GetTableNamesFromAlias(tableAlias);
				if (hasInvalidAlias)
				{
					return resultObject;
				}

				var readerAccessItems = new List<ReaderAccessItem>();

				PreProcessProperties(new PreProcessPropertyInformation
				{
					ReaderAccessItems = readerAccessItems,
					Instance = resultObject,
					RequestedTableNames = requestedTableNames
				});

				ExecuteReaderAccessItems(readerAccessItems);

				return resultObject;
			}

			var cellValue = _reader[0];

			return _dataTypeMapper.Map<T>(cellValue);
		}

		public T CreateEmptyInstance<T>()
		{
			return (T)CreateEmptyInstance(typeof(T));
		}

		private object CreateEmptyInstance(Type type)
		{
			var entity = EntityCache.GetEntity(type);
			if (entity == null)
			{
				if (Settings.RestrictToRegisteredModels)
				{
					throw new ArgumentException($"The given type is not analyzed. Engine is restricted to analyzed type. Use {nameof(TypeAnalyzer)}.{nameof(TypeAnalyzer.AddType)}<>().");
				}

				entity = TypeAnalyzer.AnalyzeType(type);
			}

			var instance = Activator.CreateInstance(type);

			foreach (var property in entity.GetProperties())
			{
				if (property.IsOwnsOne)
				{
					property.PropertyInfo.SetValue(instance, CreateEmptyInstance(property.Type));
				}
			}

			return instance;
		}

		private void PreProcessProperties(PreProcessPropertyInformation information)
		{
			information.Entity ??= EntityCache.GetEntity(information.Instance.GetType());
			if (information.Entity == null)
			{
				if (Settings.RestrictToRegisteredModels)
				{
					throw new ArgumentException($"The given type is not analyzed. Engine is restricted to analyzed type. Use {nameof(TypeAnalyzer)}.{nameof(TypeAnalyzer.AddType)}<>().");
				}

				information.Entity = TypeAnalyzer.AnalyzeType(information.Instance.GetType());
			}

			foreach (var property in information.Entity.GetProperties())
			{
				if (property.IsOwnsOne)
				{
					var ownsOneInstance = Activator.CreateInstance(property.Type);
					property.PropertyInfo.SetValue(information.Instance, ownsOneInstance);
					PreProcessProperties(new PreProcessPropertyInformation
					{
						ReaderAccessItems = information.ReaderAccessItems,
						Instance = ownsOneInstance,
						RequestedTableNames = information.RequestedTableNames,
						ColumnPrefix = property.ColumnName.ToLower() + "_",
						Entity = property.OwnsOne
					});
				}

				var columnName = information.ColumnPrefix + property.ColumnName.ToLower();
				CollectOrdinal(property, information, columnName);
			}
		}

		private void CollectOrdinal(MetaData.Models.Property property, PreProcessPropertyInformation information, string columnName)
		{
			if (!_tableAnalysisResult.ResultTableNames.Any() || !information.RequestedTableNames.Any())
			{
				// No result analyse result available
				if (!_tableAnalysisResult.ColumnCache.ContainsKey(columnName))
				{
					var columnNew = _tableAnalysisResult.ColumnCache.Keys.FirstOrDefault(c => c.EndsWith("." + columnName));
					if (!columnNew.IsNullOrEmpty())
					{
						columnName = columnNew;
					}
				}

				if (_tableAnalysisResult.ColumnCache.ContainsKey(columnName))
				{
					information.ReaderAccessItems.Add(new ReaderAccessItem
					{
						Ordinal = _tableAnalysisResult.ColumnCache[columnName].Last().Ordinal,
						Instance = information.Instance,
						PropertyInfo = property?.PropertyInfo
					});
				}

				return;
			}

			var columnFound = false;
			foreach (var requestedTableName in information.RequestedTableNames)
			{
				var fullColumnNames = requestedTableName.TableNames.Select(tableName => $"{tableName}.{columnName}").ToList();
				var fullColumnName = fullColumnNames.FirstOrDefault(f => _tableAnalysisResult.ColumnCache.ContainsKey(f));

				if (fullColumnName.IsNullOrEmpty())
				{
					continue;
				}

				columnFound = true;
				if (!_tableAnalysisResult.AliasOccurrences.ContainsKey(requestedTableName.Alias))
				{
					if (requestedTableName.TableNames.Any(t => t == requestedTableName.Alias))
					{
						// Alias is an table name
						information.ReaderAccessItems.Add(new ReaderAccessItem
						{
							Ordinal = _tableAnalysisResult.ColumnCache[fullColumnName].Last().Ordinal,
							Instance = information.Instance,
							PropertyInfo = property?.PropertyInfo
						});
					}

					// Skip property if alias is unknown

					continue;
				}

				if (!Settings.EnableValueReadingBasedOnTableAliasOccurrence)
				{
					var column = _tableAnalysisResult.ColumnCache[fullColumnName].FirstOrDefault(c => c.TableAlias == requestedTableName.Alias);
					if (column is not null)
					{
						information.ReaderAccessItems.Add(new ReaderAccessItem
						{
							Ordinal = column.Ordinal,
							Instance = information.Instance,
							PropertyInfo = property?.PropertyInfo
						});

						continue;
					}
				}

				var aliasOccurrence = _tableAnalysisResult.AliasOccurrences[requestedTableName.Alias];
				if (aliasOccurrence >= _tableAnalysisResult.ColumnCache[fullColumnName].Count)
				{
					// Skip if correct column occurrence could not be found

					continue;
				}

				information.ReaderAccessItems.Add(new ReaderAccessItem
				{
					Ordinal = _tableAnalysisResult.ColumnCache[fullColumnName][aliasOccurrence].Ordinal,
					Instance = information.Instance,
					PropertyInfo = property?.PropertyInfo
				});
			}

			if (!columnFound)
			{
				var fullColumnName = $"none.{columnName}";

				if (_tableAnalysisResult.ColumnCache.ContainsKey(fullColumnName))
				{
					information.ReaderAccessItems.Add(new ReaderAccessItem
					{
						Ordinal = _tableAnalysisResult.ColumnCache[fullColumnName].Last().Ordinal,
						Instance = information.Instance,
						PropertyInfo = property?.PropertyInfo
					});
				}
			}
		}

		private (bool HasInvalidAlias, IEnumerable<(string Alias, IList<string> TableNames)> Aliases) GetTableNamesFromAlias(string tableAlias)
		{
			if (tableAlias.IsNullOrEmpty())
			{
				return (false, Array.Empty<(string Alias, IList<string> TableNames)>());
			}

			var tableAliases = new List<(string Alias, IList<string> TableNames)>();
			foreach (var alias in tableAlias.Split(','))
			{
				var tableAliasName = alias.Trim().CleanTableAlias();

				if (_tableAnalysisResult.TableAliases.ContainsKey(tableAliasName))
				{
					tableAliases.Add((tableAliasName, _tableAnalysisResult.TableAliases[tableAliasName]));
				}
				else if (_tableAnalysisResult.TableAliases.Values.Any(t => t.Any(tableName => tableName == tableAliasName)))
				{
					tableAliases.Add((tableAliasName, new List<string> { tableAliasName }));
				}
			}

			return (tableAliases.Count == 0, tableAliases);
		}

		private void SetTableAnalysisResult(string sql)
		{
			if (sql.IsNullOrEmpty())
			{
				return;
			}

			var tableAliases = sql.GetTableAliases();
			var aliasOccurrences = CalculateTableAliasUsage(sql, tableAliases);

			var tableAnalysisResult = new TableAnalysisResult
			{
				TableAliases = tableAliases,
				AliasOccurrences = aliasOccurrences.Occurrences,
				SqlQuery = aliasOccurrences.SqlQuery
			};

			_tableAnalysisResult = tableAnalysisResult;
		}

		private (string SqlQuery, Dictionary<string, int> Occurrences) CalculateTableAliasUsage(string sql, Dictionary<string, IList<string>> tableAliases)
		{
			if (sql.IsNullOrEmpty())
			{
				return (null, null);
			}

			sql = sql.Replace("\r", "")
					 .Replace("\n", " ")
					 .Replace("\t", " ")
					 .ToLower();

			var aliasOccurrences = tableAliases
				.Select(pair => (Alias: pair.Key, Tables: String.Join("", pair.Value.OrderBy(t => t)), Index: sql.IndexOf(pair.Key + ".")))
				.GroupBy(t => t.Tables);

			var resultAliasOccurrences = new Dictionary<string, int>();
			foreach (var tableGroup in aliasOccurrences)
			{
				var aliasOccurrencesForTable = tableGroup.OrderBy(t => t.Index).ToList();
				for (var index = 0; index < aliasOccurrencesForTable.Count; index++)
				{
					resultAliasOccurrences.Add(aliasOccurrencesForTable[index].Alias, index);
				}
			}

			return (sql, resultAliasOccurrences);
		}

		private void CalculateColumnCache()
		{
			if (_tableAnalysisResult.ColumnCache != default)
			{
				return;
			}

			if (_reader.CanGetColumnSchema())
			{
				_schemaTable = _reader.GetSchemaTable();
			}

			var columnCache = new Dictionary<string, IList<ColumnCacheItem>>();
			var columnDataTypeCache = new Dictionary<string, IList<Type>>();
			var resultTableNames = new HashSet<string>();

			if (_schemaTable == default)
			{
				for (var columnOrdinal = 0; columnOrdinal < _reader.FieldCount; columnOrdinal++)
				{
					var columnName = _reader.GetName(columnOrdinal).ToLower();

					if (columnCache.ContainsKey(columnName))
					{
						if (!Settings.IgnoreDuplicatedColumns)
						{
							columnCache[columnName].Add(new ColumnCacheItem(columnOrdinal, Type.GetType(_reader.GetDataTypeName(columnOrdinal)), columnName, "*", "dbo"));
						}
					}
					else
					{
						columnCache.Add(columnName, [new ColumnCacheItem(columnOrdinal, Type.GetType(_reader.GetDataTypeName(columnOrdinal)), columnName, "*", "dbo")]);
						columnDataTypeCache.Add(columnName, []);
					}
				}
			}
			else
			{
				for (var columnOrdinal = 0; columnOrdinal < _reader.FieldCount; columnOrdinal++)
				{
					foreach (DataRow row in _schemaTable.Rows)
					{
						if (Convert.ToInt32(row["ColumnOrdinal"]) == columnOrdinal)
						{
							Type columnDataType = null;
							var dataType = row["DataType"];
							if (dataType != DBNull.Value)
							{
								columnDataType = dataType as Type;
							}

							var isExpression = Convert.ToBoolean(row["IsExpression"]);
							var schemaName = row["BaseSchemaName"]?.ToString();
							var tableName = row["BaseTableName"]?.ToString();
							var columnName = row["ColumnName"]?.ToString();

							if (schemaName.IsNullOrEmpty())
							{
								schemaName = "dbo";
							}

							if (tableName.IsNullOrEmpty())
							{
								tableName = isExpression ? "*" : "none";
							}

							if (columnName.IsNullOrEmpty())
							{
								columnName = "none";
							}

							schemaName = schemaName.ToLower();
							tableName = tableName.ToLower();
							columnName = columnName.ToLower();

							var name = $"{schemaName}.{tableName}.{columnName}";

							if (columnCache.ContainsKey(name))
							{
								columnCache[name].Add(new ColumnCacheItem(columnOrdinal, columnDataType, columnName, tableName, schemaName));
							}
							else
							{
								columnCache.Add(name, [new ColumnCacheItem(columnOrdinal, columnDataType, columnName, tableName, schemaName)]);
							}

							break;
						}
					}

					foreach (DataRow row in _schemaTable.Rows)
					{
						var schemaName = row["BaseSchemaName"]?.ToString() ?? "dbo";
						var baseTableName = row["BaseTableName"].ToString();
						var schemaAndTable = $"{schemaName}.{baseTableName}";

						if (!resultTableNames.Contains(schemaAndTable))
						{
							resultTableNames.Add(schemaAndTable);
						}
					}
				}
			}

			if (!Settings.EnableValueReadingBasedOnTableAliasOccurrence && !_tableAnalysisResult.SqlQuery.IsNullOrEmpty())
			{
				var indexOfFrom = _tableAnalysisResult.SqlQuery
					.LastIndexOf(" from ");

				var preparedQuery = _tableAnalysisResult.SqlQuery
					.Substring(0, indexOfFrom)
					.Replace("[", "")
					.Replace("]", "");

				foreach (var item in columnCache)
				{
					var firstOccurrence = item.Value[0];
					if (firstOccurrence.TableName == "none" || firstOccurrence.TableName == "*" || firstOccurrence.ColumnName == "none")
					{
						continue;
					}

					var tableAlias = _tableAnalysisResult.TableAliases
						.Where(alias => alias.Value.Contains($"{firstOccurrence.SchemaName}.{firstOccurrence.TableName}"))
						.Select(alias => alias.Key)
						.ToList();

					var collumnOccurrences = tableAlias
						.Select(alias => (Alias: alias, Index: IndexOf(preparedQuery, alias, firstOccurrence.ColumnName)))
						.Where(occurence => occurence.Index >= 0)
						.OrderBy(occurence => occurence.Index)
						.ToList();

					for (var columnIndex = 0; columnIndex < item.Value.Count; columnIndex++)
					{
						if (collumnOccurrences.Count <= columnIndex)
						{
							break;
						}

						item.Value[columnIndex].TableAlias = collumnOccurrences[columnIndex].Alias;
					}
				}
			}

			_tableAnalysisResult.ColumnCache = columnCache;
			_tableAnalysisResult.ResultTableNames = resultTableNames.ToList();
		}

		private static int IndexOf(string sql, string tableAlias, string columnName)
		{
			var columnNames = new[]{
				$" {tableAlias}.{columnName} ",
				$" {tableAlias}.{columnName},",
				$",{tableAlias}.{columnName} ",
				$",{tableAlias}.*",
				$" {tableAlias}.*"
			};

			var index = columnNames
				.Select(c => sql.IndexOf(c))
				.Max();

			return index;
		}

		private bool ShouldMapClass<T>()
		{
			return !typeof(T).IsNoClass();
		}

		private void ExecuteReaderAccessItems(IEnumerable<ReaderAccessItem> readerAccessItems)
		{
			readerAccessItems = readerAccessItems
				.Where(rai => rai.Ordinal >= 0)
				.OrderBy(rai => rai.Ordinal)
				.ToList();

			foreach (var item in readerAccessItems)
			{
				var cellValue = ExecuteReaderAccessItem(item.PropertyInfo.PropertyType, item.Ordinal);
				item.PropertyInfo.SetValue(item.Instance, _dataTypeMapper.Map(item.PropertyInfo.PropertyType, cellValue));
			}
		}

		private T ExecuteReaderAccessItem<T>(ReaderAccessItem readerAccessItem)
		{
			var type = typeof(T);
			var cellValue = ExecuteReaderAccessItem(type, readerAccessItem.Ordinal);
			if (cellValue == DBNull.Value)
			{
				return default;
			}

			return (T)_dataTypeMapper.Map(type, cellValue);
		}

		private object ExecuteReaderAccessItem(Type type, int ordinal)
		{
			type = type.GetDataType();

			if (TypeHandlerMap.Map.ContainsKey(type))
			{
				return GetValueByTypeHandler(type, TypeHandlerMap.Map[type], ordinal);
			}

			return _reader[ordinal];
		}

		private object GetValueByTypeHandler(Type type, ITypeHandler typeHandler, int ordinal)
		{
			if (!typeHandler.ReadAsByteArray)
			{
				return _reader[ordinal];
			}

			if (_reader.IsDBNull(ordinal))
			{
				return null;
			}

			var size = _reader.GetBytes(ordinal, 0, null, 0, 0);
			var result = new byte[size];
			_reader.GetBytes(ordinal, 0, result, 0, result.Length);

			return typeHandler.Parse(type, result);
		}
	}
}