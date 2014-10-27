//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2013 Antoine Aubry
    
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

using System;
using System.Globalization;

namespace EventStore.Rags.YamlDotNet.Serialization
{
	internal static class YamlFormatter
	{
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

		public static string FormatNumber(object number)
		{
			return Convert.ToString(number, numberFormat);
		}

		public static string FormatBoolean(object boolean)
		{
			return boolean.Equals(true) ? "true" : "false";
		}

		public static string FormatDateTime(object dateTime)
		{
			return ((DateTime)dateTime).ToString("o", CultureInfo.InvariantCulture);
		}

		public static string FormatTimeSpan(object timeSpan)
		{
			return ((TimeSpan)timeSpan).ToString();
		}
	}
}