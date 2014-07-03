// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) 2013 aaubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization.NodeDeserializers
{
	public sealed class ScalarNodeDeserializer : INodeDeserializer
	{
		bool INodeDeserializer.Deserialize(EventReader reader, Type expectedType, Func<EventReader, Type, object> nestedObjectDeserializer, out object value)
		{
			var scalar = reader.Allow<Scalar>();
			if (scalar == null)
			{
				value = null;
				return false;
			}

			if (expectedType.IsEnum)
			{
				value = Enum.Parse(expectedType, scalar.Value);
			}
			else
			{
				TypeCode typeCode = Type.GetTypeCode(expectedType);
				switch (typeCode)
				{
					case TypeCode.Boolean:
						value = bool.Parse(scalar.Value);
						break;

					case TypeCode.Byte:
						value = Byte.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Int16:
						value = Int16.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Int32:
						value = Int32.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Int64:
						value = Int64.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.SByte:
						value = SByte.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.UInt16:
						value = UInt16.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.UInt32:
						value = UInt32.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.UInt64:
						value = UInt64.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Single:
						value = Single.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Double:
						value = Double.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.Decimal:
						value = Decimal.Parse(scalar.Value, numberFormat);
						break;

					case TypeCode.String:
						value = scalar.Value;
						break;

					case TypeCode.Char:
						value = scalar.Value[0];
						break;

					case TypeCode.DateTime:
						// TODO: This is probably incorrect. Use the correct regular expression.
						value = DateTime.Parse(scalar.Value, CultureInfo.InvariantCulture);
						break;

					default:
						if (expectedType == typeof(object))
						{
							// Default to string
							value = scalar.Value;
						}
						else
						{
							value = TypeConverter.ChangeType(scalar.Value, expectedType);
						}
						break;
				}
			}
			return true;
		}

		private static readonly NumberFormatInfo numberFormat = new NumberFormatInfo
		{
			CurrencyDecimalSeparator = ".",
			CurrencyGroupSeparator = "_",
			CurrencyGroupSizes = new[] { 3 },
			CurrencySymbol = string.Empty,
			CurrencyDecimalDigits = 99,
			NumberDecimalSeparator = ".",
			NumberGroupSeparator = "_",
			NumberGroupSizes = new[] { 3 },
			NumberDecimalDigits = 99
		};
	}
}
