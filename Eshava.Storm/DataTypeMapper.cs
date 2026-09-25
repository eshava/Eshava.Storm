using System;
using System.Globalization;
using Eshava.Storm.Extensions;
using Eshava.Storm.Models;

namespace Eshava.Storm
{
	internal class DataTypeMapper
	{
		private static readonly Type _typeOfGuid = typeof(Guid);
		private static readonly Type _typeOfTimeSpan = typeof(TimeSpan);
		private static readonly Type _typeOfDateTimeOffset = typeof(DateTimeOffset);

		public T Map<T>(object value)
		{
			var mappedValue = Map(typeof(T), value);

			return (T)mappedValue;
		}

		public object Map(Type type, object value)
		{
			if (value == null || value == DBNull.Value)
			{
				if (type.IsValueType && !type.IsDataTypeNullable())
				{
					return Activator.CreateInstance(type);
				}

				return null;
			}

			type = type.GetDataType();

			// A registered handler wins over every built-in conversion, the same way it does when writing
			if (TypeHandlerMap.Map.TryGetValue(type, out var handler))
			{
				return handler.Parse(type, value);
			}

			if (type.IsEnum)
			{
				return MapEnum(type, value);
			}

			if (type.IsInstanceOfType(value))
			{
				return value;
			}

			if (TryConvertSpecialType(type, value, out var convertedValue))
			{
				return convertedValue;
			}

			return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
		}

		private static object MapEnum(Type type, object value)
		{
			if (value is string text)
			{
				return Enum.Parse(type, text);
			}

			// Converted through the underlying type, so an enum based on long keeps its full range
			var underlyingValue = Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture);

			return Enum.ToObject(type, underlyingValue);
		}

		/// <summary>
		/// Covers the types Convert.ChangeType does not know, as a provider returns them when it stores them as text or bytes
		/// </summary>
		private static bool TryConvertSpecialType(Type type, object value, out object convertedValue)
		{
			convertedValue = null;

			if (type == _typeOfGuid)
			{
				convertedValue = value is byte[] bytes ? new Guid(bytes) : Guid.Parse(value.ToString());
			}
			else if (type == _typeOfTimeSpan && value is string timeSpanText)
			{
				convertedValue = TimeSpan.Parse(timeSpanText, CultureInfo.InvariantCulture);
			}
			else if (type == _typeOfDateTimeOffset && value is string dateTimeOffsetText)
			{
				convertedValue = DateTimeOffset.Parse(dateTimeOffsetText, CultureInfo.InvariantCulture);
			}
#if NET6_0_OR_GREATER
			else if (type == typeof(DateOnly))
			{
				convertedValue = value switch
				{
					DateTime dateTime => DateOnly.FromDateTime(dateTime),
					string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
					_ => null
				};
			}
			else if (type == typeof(TimeOnly))
			{
				convertedValue = value switch
				{
					TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
					DateTime dateTime => TimeOnly.FromDateTime(dateTime),
					string text => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
					_ => null
				};
			}
#endif

			return convertedValue != null;
		}
	}
}