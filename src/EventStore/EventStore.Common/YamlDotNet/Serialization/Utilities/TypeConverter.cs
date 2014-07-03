//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2011, 2012, 2013 Antoine Aubry

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

// Remarks: This file is imported from the SixPack library. This is ok because
// the copyright holder has agreed to redistribute this file under the license
// used in YamlDotNet.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace YamlDotNet.Serialization.Utilities
{
	/// <summary>
	/// Performs type conversions using every standard provided by the .NET library.
	/// </summary>
	public static class TypeConverter
	{
		/// <summary>
		/// Registers a <see cref="System.ComponentModel.TypeConverter"/> dynamically.
		/// </summary>
		/// <typeparam name="TConvertible">The type to which the coverter should be associated.</typeparam>
		/// <typeparam name="TConverter">The type of the converter.</typeparam>
		public static void RegisterTypeConverter<TConvertible, TConverter>()
			where TConverter : System.ComponentModel.TypeConverter
		{
			var alreadyRegistered = TypeDescriptor.GetAttributes(typeof(TConvertible))
				.OfType<TypeConverterAttribute>()
				.Any(a => a.ConverterTypeName == typeof(TConverter).AssemblyQualifiedName);

			if (!alreadyRegistered)
			{
				TypeDescriptor.AddAttributes(typeof(TConvertible), new TypeConverterAttribute(typeof(TConverter)));
			}
		}

		/// <summary>
		/// Converts the specified value.
		/// </summary>
		/// <typeparam name="T">The type to which the value is to be converted.</typeparam>
		/// <param name="value">The value to convert.</param>
		/// <returns></returns>
		public static T ChangeType<T>(object value)
		{
			return (T)ChangeType(value, typeof(T));
		}

		/// <summary>
		/// Converts the specified value.
		/// </summary>
		/// <typeparam name="T">The type to which the value is to be converted.</typeparam>
		/// <param name="value">The value to convert.</param>
		/// <param name="provider">The provider.</param>
		/// <returns></returns>
		public static T ChangeType<T>(object value, IFormatProvider provider)
		{
			return (T)ChangeType(value, typeof(T), provider);
		}

		/// <summary>
		/// Converts the specified value.
		/// </summary>
		/// <typeparam name="T">The type to which the value is to be converted.</typeparam>
		/// <param name="value">The value to convert.</param>
		/// <param name="culture">The culture.</param>
		/// <returns></returns>
		public static T ChangeType<T>(object value, CultureInfo culture)
		{
			return (T)ChangeType(value, typeof(T), culture);
		}

		/// <summary>
		/// Converts the specified value using the invariant culture.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="destinationType">The type to which the value is to be converted.</param>
		/// <returns></returns>
		public static object ChangeType(object value, Type destinationType)
		{
			return ChangeType(value, destinationType, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Converts the specified value.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="destinationType">The type to which the value is to be converted.</param>
		/// <param name="provider">The format provider.</param>
		/// <returns></returns>
		public static object ChangeType(object value, Type destinationType, IFormatProvider provider)
		{
			return ChangeType(value, destinationType, new CultureInfoAdapter(CultureInfo.CurrentCulture, provider));
		}

		/// <summary>
		/// Converts the specified value.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="destinationType">The type to which the value is to be converted.</param>
		/// <param name="culture">The culture.</param>
		/// <returns></returns>
		public static object ChangeType(object value, Type destinationType, CultureInfo culture)
		{
			// Handle null and DBNull
			if (value == null || value is DBNull)
			{
				return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
			}

			var sourceType = value.GetType();

			// If the source type is compatible with the destination type, no conversion is needed
			if (destinationType.IsAssignableFrom(sourceType))
			{
				return value;
			}

			// Nullable types get a special treatment
			if (destinationType.IsGenericType)
			{
				var genericTypeDefinition = destinationType.GetGenericTypeDefinition();
				if (genericTypeDefinition == typeof(Nullable<>))
				{
					var innerType = destinationType.GetGenericArguments()[0];
					var convertedValue = ChangeType(value, innerType, culture);
					return Activator.CreateInstance(destinationType, convertedValue);
				}
			}

			// Enums also require special handling
			if (destinationType.IsEnum)
			{
				var valueText = value as string;
				return valueText != null ? Enum.Parse(destinationType, valueText, true) : value;
			}

			// Special case for booleans to support parsing "1" and "0". This is
			// necessary for compatibility with XML Schema.
			if (destinationType == typeof(bool))
			{
				if ("0".Equals(value))
					return false;

				if ("1".Equals(value))
					return true;
			}

			// Try with the source type's converter
			var sourceConverter = TypeDescriptor.GetConverter(value);
			if (sourceConverter != null && sourceConverter.CanConvertTo(destinationType))
			{
				return sourceConverter.ConvertTo(null, culture, value, destinationType);
			}

			// Try with the destination type's converter
			var destinationConverter = TypeDescriptor.GetConverter(destinationType);
			if (destinationConverter != null && destinationConverter.CanConvertFrom(sourceType))
			{
				return destinationConverter.ConvertFrom(null, culture, value);
			}

			// Try to find a casting operator in the source or destination type
			foreach (var type in new[] { sourceType, destinationType })
			{
				foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
				{
					var isCastingOperator =
						method.IsSpecialName &&
						(method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
						destinationType.IsAssignableFrom(method.ReturnParameter.ParameterType);

					if (isCastingOperator)
					{
						var parameters = method.GetParameters();

						var isCompatible =
							parameters.Length == 1 &&
							parameters[0].ParameterType.IsAssignableFrom(sourceType);

						if (isCompatible)
						{
							try
							{
								return method.Invoke(null, new[] { value });
							}
							catch (TargetInvocationException ex)
							{
								throw ex.Unwrap();
							}
						}
					}
				}
			}

			// If source type is string, try to find a Parse or TryParse method
			if (sourceType == typeof(string))
			{
				try
				{
					// Try with - public static T Parse(string, IFormatProvider)
					var parseMethod = destinationType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(IFormatProvider) }, null);
					if (parseMethod != null)
					{
						return parseMethod.Invoke(null, new object[] { value, culture });
					}

					// Try with - public static T Parse(string)
					parseMethod = destinationType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
					if (parseMethod != null)
					{
						return parseMethod.Invoke(null, new object[] { value });
					}
				}
				catch (TargetInvocationException ex)
				{
					throw ex.Unwrap();
				}
			}

			// Handle TimeSpan
			if (destinationType == typeof(TimeSpan))
			{
				return TimeSpan.Parse((string)ChangeType(value, typeof(string), CultureInfo.InvariantCulture));
			}

			// Default to the Convert class
			return Convert.ChangeType(value, destinationType, CultureInfo.InvariantCulture);
		}

		private class CultureInfoAdapter : CultureInfo
		{
			private readonly IFormatProvider _provider;

			public CultureInfoAdapter(CultureInfo baseCulture, IFormatProvider provider)
				: base(baseCulture.LCID)
			{
				_provider = provider;
			}

			public override object GetFormat(Type formatType)
			{
				return _provider.GetFormat(formatType);
			}
		}

		/// <summary>
		/// Tries to parse the specified value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">The value.</param>
		/// <returns></returns>
		public static T? TryParse<T>(string value) where T : struct
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
				case TypeCode.Boolean:
					return (T?)(object)TryParse<bool>(value, bool.TryParse);

				case TypeCode.Byte:
					return (T?)(object)TryParse<byte>(value, byte.TryParse);

				case TypeCode.DateTime:
					return (T?)(object)TryParse<DateTime>(value, DateTime.TryParse);

				case TypeCode.Decimal:
					return (T?)(object)TryParse<decimal>(value, decimal.TryParse);

				case TypeCode.Double:
					return (T?)(object)TryParse<double>(value, double.TryParse);

				case TypeCode.Int16:
					return (T?)(object)TryParse<short>(value, short.TryParse);

				case TypeCode.Int32:
					return (T?)(object)TryParse<int>(value, int.TryParse);

				case TypeCode.Int64:
					return (T?)(object)TryParse<long>(value, long.TryParse);

				case TypeCode.SByte:
					return (T?)(object)TryParse<sbyte>(value, sbyte.TryParse);

				case TypeCode.Single:
					return (T?)(object)TryParse<float>(value, float.TryParse);

				case TypeCode.UInt16:
					return (T?)(object)TryParse<ushort>(value, ushort.TryParse);

				case TypeCode.UInt32:
					return (T?)(object)TryParse<uint>(value, uint.TryParse);

				case TypeCode.UInt64:
					return (T?)(object)TryParse<ulong>(value, ulong.TryParse);

				default:
					throw new NotSupportedException(string.Format("Cannot parse type '{0}'.", typeof(T).FullName));
			}
		}

		/// <summary>
		/// Tries to parse the specified value.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">The value to be parsed.</param>
		/// <param name="parse">The parse function.</param>
		/// <returns></returns>
		public static T? TryParse<T>(string value, TryParseDelegate<T> parse) where T : struct
		{
			T result;
			return parse(value, out result) ? (T?)result : null;
		}

		/// <summary>
		/// Defines a method that is used to tentatively parse a string.
		/// </summary>
		public delegate bool TryParseDelegate<T>(string value, out T result);
	}
}