using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Eshava.Storm.Constants;
using Eshava.Storm.Extensions;
using Eshava.Storm.Interfaces;
using Eshava.Storm.Models;

namespace Eshava.Storm
{
	internal class ParameterCollector
	{
		private readonly Dictionary<string, ParameterInfo> _parameters = new Dictionary<string, ParameterInfo>();

		/// <summary>
		/// Construct a parameter collector
		/// </summary>
		public ParameterCollector()
		{

		}

		/// <summary>
		/// Construct a parameter collector
		/// </summary>
		/// <param name="parameter">Can be an anonymous type or a <see cref="ParameterCollector"/></param>
		public ParameterCollector(object parameter)
		{
			AddParameter(parameter);
		}

		public void AddParameter(object parameter)
		{
			if (parameter as object == null)
			{
				return;
			}

			var collector = parameter as ParameterCollector;
			if (collector != null)
			{
				if ((collector._parameters?.Count ?? 0) == 0)
				{
					return;
				}

				foreach (var internalParameter in collector._parameters)
				{
					Add(internalParameter);
				}

				return;
			}

			var dictionary = parameter as IEnumerable<KeyValuePair<string, object>>;
			if (dictionary != null)
			{
				foreach (var keyValuePair in dictionary)
				{
					Add(keyValuePair.Key, keyValuePair.Value, null, null, null);
				}

				return;
			}

			var parameterPropertyInfos = parameter.GetType().GetProperties();
			foreach (var propertyInfo in parameterPropertyInfos)
			{
				Add(propertyInfo.Name, propertyInfo.GetValue(parameter), null, null, null);
			}
		}

		public void Add(string name, object value, DbType? dbType, ParameterDirection? direction, int? size)
		{
			_parameters[name.Clean()] = new ParameterInfo
			{
				Name = name,
				Value = value,
				ParameterDirection = direction ?? ParameterDirection.Input,
				DbType = dbType,
				Size = size
			};
		}

		public void AddParameters(IDbCommand command)
		{
			foreach (var parameter in _parameters.Values)
			{
				AddParameter(parameter, command);
			}
		}

		private void AddParameter(ParameterInfo parameter, IDbCommand command)
		{
			var dbType = parameter.DbType;
			var parameterName = parameter.Name.Clean();

			if (parameter.Value is ICustomQueryParameter)
			{
				((ICustomQueryParameter)parameter.Value).AddParameter(command, parameterName);

				return;
			}

			ITypeHandler handler = null;

			if (dbType == null && parameter.Value != null)
			{
				dbType = parameter.Value.GetType().LookupDbType(parameterName, true, out handler);
			}

			var valueType = parameter.Value?.GetType();
			if (handler == null && valueType != null && !valueType.IsByteArray() && (valueType.ImplementsIEnumerable() || valueType.IsArray))
			{
				AddEnumerationParameter(parameter, command, dbType);

				return;
			}

			var addParameter = !command.Parameters.Contains(parameterName);
			var dataParameter = GetOrCreateDataParameter(command, parameterName, addParameter);

			if (handler == null)
			{
				dataParameter.Value = parameter.Value.SanitizeParameterValue();
				if (dbType != null && dataParameter.DbType != dbType)
				{
					dataParameter.DbType = dbType.Value;
				}

				var s = parameter.Value as string;
				if (s?.Length <= DefaultValues.DBSTRINGDEFAULTLENGTH)
				{
					dataParameter.Size = DefaultValues.DBSTRINGDEFAULTLENGTH;
				}

				SetBasicParameterInfos(parameter, dataParameter);
			}
			else
			{
				if (dbType != null)
				{
					dataParameter.DbType = dbType.Value;
				}

				SetBasicParameterInfos(parameter, dataParameter);

				handler.SetValue(dataParameter, parameter.Value ?? DBNull.Value);
			}

			if (addParameter)
			{
				command.Parameters.Add(dataParameter);
			}
		}

		private void AddEnumerationParameter(ParameterInfo parameter, IDbCommand command, DbType? dbType)
		{
			var parameterNames = new List<string>();
			var valueEnumerable = (IEnumerable)parameter.Value;
			var index = 0;

			foreach (var value in valueEnumerable)
			{
				var newParameter = new ParameterInfo
				{
					Name = $"{parameter.Name}_p{index}",
					Value = value,
					// An element with a type handler has to look it up itself, a given DbType would skip that lookup
					DbType = value != null && value.GetType().HasTypeHandler() ? null : dbType,
					ParameterDirection = parameter.ParameterDirection
				};

				parameterNames.Add(newParameter.Name);
				AddParameter(newParameter, command);
				index++;
			}

			if (parameterNames.Count > 0)
			{
				command.CommandText = command.CommandText.Replace("@" + parameter.Name, $"({String.Join(",", parameterNames.Select(name => "@" + name))})");
			}
		}

		private void SetBasicParameterInfos(ParameterInfo parameter, IDbDataParameter dataParameter)
		{
			dataParameter.Direction = parameter.ParameterDirection;

			if (parameter.Size != null)
			{
				dataParameter.Size = parameter.Size.Value;
			}

			if (parameter.Precision != null)
			{
				dataParameter.Precision = parameter.Precision.Value;
			}

			if (parameter.Scale != null)
			{
				dataParameter.Scale = parameter.Scale.Value;
			}
		}

		private IDbDataParameter GetOrCreateDataParameter(IDbCommand command, string parameterName, bool addParameter)
		{
			var dataParameter = default(IDbDataParameter);

			if (addParameter)
			{
				dataParameter = command.CreateParameter();
				dataParameter.ParameterName = parameterName;
			}
			else
			{
				dataParameter = (IDbDataParameter)command.Parameters[parameterName];
			}

			return dataParameter;
		}

		private void Add(KeyValuePair<string, ParameterInfo> keyValuePair)
		{
			if (_parameters.ContainsKey(keyValuePair.Key))
			{
				_parameters[keyValuePair.Key] = keyValuePair.Value;
			}
			else
			{
				_parameters.Add(keyValuePair.Key, keyValuePair.Value);
			}
		}
	}
}