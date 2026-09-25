using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Eshava.Storm.Dialects;
using Eshava.Storm.Enums;
using Eshava.Storm.Extensions;
using Eshava.Storm.Interfaces;
using Eshava.Storm.MetaData;
using Eshava.Storm.MetaData.Models;
using Eshava.Storm.Models;

namespace Eshava.Storm
{
	internal class ObjectMapper : IObjectMapper, IObjectGenerator
	{
		private const int TEXTANALYSISCACHESIZE = 500;
		private const string UNBOUNDTABLENAME = "none";
		private const string EXPRESSIONTABLENAME = "*";

		// The analysis of a statement text depends on the text only, so it is shared by every reader of the same statement
		// The dialect is part of the key: it decides the default schema the analysis assumes
		private static readonly ConcurrentDictionary<(SqlDialect Dialect, string Sql), (Dictionary<string, IList<string>> TableAliases, Dictionary<string, int> AliasOccurrences, string SqlQuery)> _textAnalysisCache
			= new ConcurrentDictionary<(SqlDialect Dialect, string Sql), (Dictionary<string, IList<string>> TableAliases, Dictionary<string, int> AliasOccurrences, string SqlQuery)>();

		private readonly DbDataReader _reader;
		private readonly DataTypeMapper _dataTypeMapper;
		private readonly Dictionary<(Type Type, string TableAlias), MappingPlan> _mappingPlans = new Dictionary<(Type Type, string TableAlias), MappingPlan>();
		private readonly Dictionary<(string ColumnName, string TableAlias), int> _valueOrdinals = new Dictionary<(string ColumnName, string TableAlias), int>();
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

			return FindColumns(columnName?.ToLowerInvariant() ?? "", requestedTableNames).FirstOrDefault()?.DataType;
		}

		public T GetValue<T>(string columnName, string tableAlias = null)
		{
			var valueType = typeof(T).GetDataType();

			if ((ShouldMapClass<T>() && !TypeHandlerMap.Map.ContainsKey(valueType)) || columnName.IsNullOrEmpty())
			{
				return default;
			}

			CalculateColumnCache();

			var key = (columnName, tableAlias ?? "");
			if (!_valueOrdinals.TryGetValue(key, out var ordinal))
			{
				(var hasInvalidAlias, var requestedTableNames) = GetTableNamesFromAlias(tableAlias);
				ordinal = hasInvalidAlias
					? -1
					: FindColumns(columnName.ToLowerInvariant(), requestedTableNames).FirstOrDefault()?.Ordinal ?? -1;

				_valueOrdinals.Add(key, ordinal);
			}

			if (ordinal < 0)
			{
				return default;
			}

			var type = typeof(T);
			var cellValue = ReadCell(type, ordinal);
			if (cellValue == DBNull.Value)
			{
				return default;
			}

			return (T)_dataTypeMapper.Map(type, cellValue);
		}

		public T Map<T>(string tableAlias = null)
		{
			if (ShouldMapClass<T>())
			{
				CalculateColumnCache();

				var key = (typeof(T), tableAlias ?? "");
				if (!_mappingPlans.TryGetValue(key, out var mappingPlan))
				{
					(var hasInvalidAlias, var requestedTableNames) = GetTableNamesFromAlias(tableAlias);

					// An alias the statement does not know maps to an empty object
					mappingPlan = hasInvalidAlias ? null : CreateMappingPlan(typeof(T), requestedTableNames);
					_mappingPlans.Add(key, mappingPlan);
				}

				if (mappingPlan == null)
				{
					return Activator.CreateInstance<T>();
				}

				return (T)ExecuteMappingPlan(mappingPlan);
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
			var entity = TypeAnalyzer.GetOrAnalyzeEntity(type);
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

		private MappingPlan CreateMappingPlan(Type type, IEnumerable<(string Alias, IList<string> TableNames)> requestedTableNames)
		{
			var mappingPlan = new MappingPlan(type);
			AddToMappingPlan(mappingPlan, 0, TypeAnalyzer.GetOrAnalyzeEntity(type), "", requestedTableNames);

			// Read in column order, which a sequential reader requires
			mappingPlan.Values.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));

			return mappingPlan;
		}

		private void AddToMappingPlan(MappingPlan mappingPlan, int slot, AbstractEntity entity, string columnPrefix, IEnumerable<(string Alias, IList<string> TableNames)> requestedTableNames)
		{
			foreach (var property in entity.GetProperties())
			{
				if (property.IsOwnsOne)
				{
					mappingPlan.OwnedObjects.Add((slot, property.PropertyInfo, property.Type));

					// The same prefix the insert and update write the columns of an owned object with
					var ownedSlot = mappingPlan.OwnedObjects.Count;
					AddToMappingPlan(mappingPlan, ownedSlot, property.OwnsOne, columnPrefix + property.Name.ToLowerInvariant() + "_", requestedTableNames);

					continue;
				}

				var columnName = columnPrefix + property.ColumnName.ToLowerInvariant();
				foreach (var column in FindColumns(columnName, requestedTableNames))
				{
					mappingPlan.Values.Add((slot, property.PropertyInfo, column.Ordinal));
				}
			}
		}

		private object ExecuteMappingPlan(MappingPlan mappingPlan)
		{
			var instances = new object[mappingPlan.OwnedObjects.Count + 1];
			instances[0] = Activator.CreateInstance(mappingPlan.Type);

			for (var index = 0; index < mappingPlan.OwnedObjects.Count; index++)
			{
				var ownedObject = mappingPlan.OwnedObjects[index];
				var instance = Activator.CreateInstance(ownedObject.Type);
				ownedObject.Property.SetValue(instances[ownedObject.ParentSlot], instance);
				instances[index + 1] = instance;
			}

			foreach (var value in mappingPlan.Values)
			{
				var propertyType = value.Property.PropertyType;
				var cellValue = ReadCell(propertyType, value.Ordinal);
				value.Property.SetValue(instances[value.Slot], _dataTypeMapper.Map(propertyType, cellValue));
			}

			return instances[0];
		}

		/// <summary>
		/// The columns a property or value is read from, in the order they are applied
		/// </summary>
		private IEnumerable<ColumnCacheItem> FindColumns(string columnName, IEnumerable<(string Alias, IList<string> TableNames)> requestedTableNames)
		{
			var columnCache = _tableAnalysisResult.ColumnCache;

			if (!_tableAnalysisResult.ResultTableNames.Any() || !requestedTableNames.Any())
			{
				// No result analyse result available
				var key = columnCache.ContainsKey(columnName)
					? columnName
					: columnCache.Keys.FirstOrDefault(c => c.EndsWith("." + columnName));

				if (!key.IsNullOrEmpty())
				{
					yield return SelectDuplicate(columnCache[key]);
				}

				yield break;
			}

			var columnFound = false;
			foreach (var requestedTableName in requestedTableNames)
			{
				var fullColumnName = requestedTableName.TableNames
					.Select(tableName => $"{tableName}.{columnName}")
					.FirstOrDefault(columnCache.ContainsKey);

				if (fullColumnName.IsNullOrEmpty())
				{
					continue;
				}

				columnFound = true;
				var columns = columnCache[fullColumnName];

				if (!_tableAnalysisResult.AliasOccurrences.ContainsKey(requestedTableName.Alias))
				{
					if (IsTableName(requestedTableName.Alias, requestedTableName.TableNames))
					{
						yield return columns.Last();
					}

					// Skip property if alias is unknown

					continue;
				}

				if (!Settings.EnableValueReadingBasedOnTableAliasOccurrence)
				{
					var column = columns.FirstOrDefault(c => c.TableAlias == requestedTableName.Alias);
					if (column is not null)
					{
						yield return column;

						continue;
					}
				}

				var aliasOccurrence = _tableAnalysisResult.AliasOccurrences[requestedTableName.Alias];
				if (aliasOccurrence >= columns.Count)
				{
					// Skip if correct column occurrence could not be found

					continue;
				}

				var columnCacheItem = columns[aliasOccurrence];
				if (!columnCacheItem.TableAlias.IsNullOrEmpty()
					&& !requestedTableName.Alias.IsNullOrEmpty()
					&& columnCacheItem.TableAlias != requestedTableName.Alias
				)
				{
					continue;
				}

				yield return columnCacheItem;
			}

			if (!columnFound)
			{
				// A column that belongs to no table, a computed one for instance
				var unboundColumns = columnCache.Values.FirstOrDefault(columns =>
					columns[0].ColumnName == columnName
					&& (columns[0].TableName == UNBOUNDTABLENAME || columns[0].TableName == EXPRESSIONTABLENAME)
				);

				if (unboundColumns != null)
				{
					yield return unboundColumns.Last();
				}
			}
		}

		/// <summary>
		/// Without a table alias, a duplicated column is read from its last occurrence, or its first one if duplicates are ignored
		/// </summary>
		private static ColumnCacheItem SelectDuplicate(IList<ColumnCacheItem> columns)
		{
			return Settings.IgnoreDuplicatedColumns ? columns.First() : columns.Last();
		}

		private static bool IsTableName(string alias, IList<string> tableNames)
		{
			return tableNames.Any(tableName => tableName == alias || tableName.EndsWith("." + alias));
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

					continue;
				}

				// A table name used as alias
				var tableNames = _tableAnalysisResult.TableAliases.Values
					.SelectMany(names => names)
					.Where(tableName => IsTableName(tableAliasName, new[] { tableName }))
					.Distinct()
					.ToList();

				if (tableNames.Count > 0)
				{
					tableAliases.Add((tableAliasName, tableNames));
				}
			}

			return (tableAliases.Count == 0, tableAliases);
		}

		private void SetTableAnalysisResult(string sql)
		{
			var textAnalysis = sql.IsNullOrEmpty()
				? (new Dictionary<string, IList<string>>(), new Dictionary<string, int>(), null)
				: GetTextAnalysis(sql);

			_tableAnalysisResult = new TableAnalysisResult
			{
				TableAliases = textAnalysis.TableAliases,
				AliasOccurrences = textAnalysis.AliasOccurrences,
				SqlQuery = textAnalysis.SqlQuery
			};
		}

		private static (Dictionary<string, IList<string>> TableAliases, Dictionary<string, int> AliasOccurrences, string SqlQuery) GetTextAnalysis(string sql)
		{
			var cacheKey = (Settings.Dialect, sql);
			if (_textAnalysisCache.TryGetValue(cacheKey, out var textAnalysis))
			{
				return textAnalysis;
			}

			var normalizedSql = sql.NormalizeQuotedIdentifiers();
			var tableAliases = normalizedSql.GetTableAliases();
			var aliasOccurrences = CalculateTableAliasUsage(normalizedSql, tableAliases);
			textAnalysis = (tableAliases, aliasOccurrences.Occurrences, aliasOccurrences.SqlQuery);

			// Bounded, since a statement text can contain values and the number of texts is not
			if (_textAnalysisCache.Count >= TEXTANALYSISCACHESIZE)
			{
				_textAnalysisCache.Clear();
			}

			_textAnalysisCache.TryAdd(cacheKey, textAnalysis);

			return textAnalysis;
		}

		private static (string SqlQuery, Dictionary<string, int> Occurrences) CalculateTableAliasUsage(string sql, Dictionary<string, IList<string>> tableAliases)
		{
			sql = sql.Replace("\r", "")
					 .Replace("\n", " ")
					 .Replace("\t", " ")
					 .ToLowerInvariant();

			var aliasOccurrences = tableAliases
				.Select(pair => (Alias: pair.Key, Tables: String.Join("", pair.Value.OrderBy(t => t)), Index: IndexOfAlias(sql, pair.Key)))
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

		/// <summary>
		/// The first use of an alias as a qualifier; the alias e must not be found in le.
		/// </summary>
		private static int IndexOfAlias(string sql, string alias)
		{
			var match = Regex.Match(sql, $@"(?<![\p{{L}}\p{{N}}_$#@.\]]){Regex.Escape(alias)}\.", RegexOptions.CultureInvariant);

			return match.Success ? match.Index : -1;
		}

		private void CalculateColumnCache()
		{
			if (_tableAnalysisResult.ColumnCache != default)
			{
				return;
			}

			_schemaTable = GetSchemaTableIfAvailable();

			var columnCache = new Dictionary<string, IList<ColumnCacheItem>>();
			var resultTableNames = new HashSet<string>();

			if (_schemaTable == default)
			{
				for (var columnOrdinal = 0; columnOrdinal < _reader.FieldCount; columnOrdinal++)
				{
					var columnName = _reader.GetName(columnOrdinal).ToLowerInvariant();
					var columnCacheItem = new ColumnCacheItem(columnOrdinal, _reader.GetFieldType(columnOrdinal), columnName, EXPRESSIONTABLENAME, SqlDialects.Current.DefaultSchema);

					if (columnCache.ContainsKey(columnName))
					{
						columnCache[columnName].Add(columnCacheItem);
					}
					else
					{
						columnCache.Add(columnName, [columnCacheItem]);
					}
				}
			}
			else
			{
				var schemaRows = _schemaTable.Rows
					.Cast<DataRow>()
					.GroupBy(row => Convert.ToInt32(row["ColumnOrdinal"]))
					.ToDictionary(group => group.Key, group => group.First());

				for (var columnOrdinal = 0; columnOrdinal < _reader.FieldCount; columnOrdinal++)
				{
					if (!schemaRows.TryGetValue(columnOrdinal, out var row))
					{
						continue;
					}

					// Not every provider reports hidden columns, SQLite for one has no such column in its schema table
					var isHidden = GetSchemaValue(row, "IsHidden") is bool hidden && hidden;
					if (isHidden)
					{
						continue;
					}

					var columnDataType = GetSchemaValue(row, "DataType") as Type;
					var isExpression = GetSchemaValue(row, "IsExpression") is bool expression && expression;
					var schemaName = GetSchemaValue(row, "BaseSchemaName")?.ToString();
					var tableName = GetSchemaValue(row, "BaseTableName")?.ToString();
					var columnName = GetSchemaValue(row, "ColumnName")?.ToString();
					var baseColumnName = GetSchemaValue(row, "BaseColumnName")?.ToString();

					if (schemaName.IsNullOrEmpty())
					{
						schemaName = SqlDialects.Current.DefaultSchema;
					}

					if (tableName.IsNullOrEmpty())
					{
						tableName = isExpression ? EXPRESSIONTABLENAME : UNBOUNDTABLENAME;
					}

					if (columnName.IsNullOrEmpty())
					{
						columnName = UNBOUNDTABLENAME;
					}

					schemaName = schemaName.ToLowerInvariant();
					tableName = tableName.ToLowerInvariant();
					columnName = columnName.ToLowerInvariant();
					baseColumnName = baseColumnName.IsNullOrEmpty() ? columnName : baseColumnName.ToLowerInvariant();

					var name = $"{schemaName}.{tableName}.{columnName}";
					var columnCacheItem = new ColumnCacheItem(columnOrdinal, columnDataType, columnName, tableName, schemaName, baseColumnName);

					if (columnCache.ContainsKey(name))
					{
						columnCache[name].Add(columnCacheItem);
					}
					else
					{
						columnCache.Add(name, [columnCacheItem]);
					}
				}

				foreach (DataRow row in _schemaTable.Rows)
				{
					var schemaName = GetSchemaValue(row, "BaseSchemaName")?.ToString() ?? SqlDialects.Current.DefaultSchema;
					resultTableNames.Add($"{schemaName}.{GetSchemaValue(row, "BaseTableName")}");
				}
			}

			if (!Settings.EnableValueReadingBasedOnTableAliasOccurrence && !_tableAnalysisResult.SqlQuery.IsNullOrEmpty())
			{
				AssignTableAliases(columnCache);
			}

			_tableAnalysisResult.ColumnCache = columnCache;
			_tableAnalysisResult.ResultTableNames = resultTableNames.ToList();
		}

		/// <summary>
		/// Assigns each occurrence of a column to the table alias it is selected with, in the order of the select list
		/// </summary>
		private void AssignTableAliases(Dictionary<string, IList<ColumnCacheItem>> columnCache)
		{
			// The trailing blank ends the last column of the list
			var preparedQuery = _tableAnalysisResult.SqlQuery
				.GetMainSelectList()
				.Replace("[", "")
				.Replace("]", "")
				+ " ";

			foreach (var item in columnCache)
			{
				var firstOccurrence = item.Value[0];
				if (firstOccurrence.TableName == UNBOUNDTABLENAME || firstOccurrence.TableName == EXPRESSIONTABLENAME || firstOccurrence.ColumnName == UNBOUNDTABLENAME)
				{
					continue;
				}

				var tableAlias = _tableAnalysisResult.TableAliases
					.Where(alias => alias.Value.Contains($"{firstOccurrence.SchemaName}.{firstOccurrence.TableName}"))
					.Select(alias => alias.Key)
					.ToList();

				var collumnOccurrences = tableAlias
					.Select(alias => (Alias: alias, Index: IndexOfColumn(preparedQuery, alias, firstOccurrence)))
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

		/// <summary>
		/// The first position the select list reads the column through the alias: by name, through <c>alias.*</c>,
		/// or, for a column renamed in the select list, as <c>alias.BaseColumn AS Name</c>
		/// </summary>
		private static int IndexOfColumn(string selectList, string tableAlias, ColumnCacheItem column)
		{
			// A column selected under another name is a different column of the result, so a renamed use does not count
			var columnPattern = column.BaseColumnName == column.ColumnName
				? $@"({Regex.Escape(column.ColumnName)}(?![\p{{L}}\p{{N}}_$#@])(?!\s+(as\s+)?[\p{{L}}_""]\S*\s*(,|$))|\*)"
				: $@"{Regex.Escape(column.BaseColumnName)}\s+(as\s+)?""?{Regex.Escape(column.ColumnName)}""?(?![\p{{L}}\p{{N}}_$#@])";

			var match = Regex.Match(
				selectList,
				$@"(?<![\p{{L}}\p{{N}}_$#@.]){Regex.Escape(tableAlias)}\.{columnPattern}",
				RegexOptions.CultureInvariant
			);

			return match.Success ? match.Index : -1;
		}

		private DataTable GetSchemaTableIfAvailable()
		{
			try
			{
				// Every DbDataReader claims to support a column schema, but a reader without one throws
				return _reader.CanGetColumnSchema() ? _reader.GetSchemaTable() : null;
			}
			catch (NotSupportedException)
			{
				return null;
			}
		}

		private static object GetSchemaValue(DataRow row, string columnName)
		{
			if (!row.Table.Columns.Contains(columnName))
			{
				return null;
			}

			var value = row[columnName];

			return value == DBNull.Value ? null : value;
		}

		private bool ShouldMapClass<T>()
		{
			return !typeof(T).IsNoClass();
		}

		private object ReadCell(Type type, int ordinal)
		{
			type = type.GetDataType();

			if (TypeHandlerMap.Map.TryGetValue(type, out var typeHandler))
			{
				return GetValueByTypeHandler(typeHandler, ordinal);
			}

			return _reader[ordinal];
		}

		private object GetValueByTypeHandler(ITypeHandler typeHandler, int ordinal)
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

			// Only read here: the handler parses the bytes once, in the data type mapper
			return result;
		}
	}
}