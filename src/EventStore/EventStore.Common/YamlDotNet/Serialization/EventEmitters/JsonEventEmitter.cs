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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Serialization.EventEmitters
{
	public sealed class JsonEventEmitter : ChainedEventEmitter
	{
		public JsonEventEmitter(IEventEmitter nextEmitter)
			: base(nextEmitter)
		{
		}

		public override void Emit(AliasEventInfo eventInfo)
		{
			throw new NotSupportedException("Aliases are not supported in JSON");
		}

		public override void Emit(ScalarEventInfo eventInfo)
		{
			eventInfo.IsPlainImplicit = true;
			eventInfo.Style = ScalarStyle.Plain;

			var typeCode = eventInfo.Source.Value != null
				? Type.GetTypeCode(eventInfo.Source.Type)
				: TypeCode.Empty;

			switch (typeCode)
			{
				case TypeCode.Boolean:
					eventInfo.RenderedValue = YamlFormatter.FormatBoolean(eventInfo.Source.Value);
					break;

				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					eventInfo.RenderedValue = YamlFormatter.FormatNumber(eventInfo.Source.Value);
					break;

				case TypeCode.String:
				case TypeCode.Char:
					eventInfo.RenderedValue = eventInfo.Source.Value.ToString();
					eventInfo.Style = ScalarStyle.DoubleQuoted;
					break;

				case TypeCode.DateTime:
					eventInfo.RenderedValue = YamlFormatter.FormatDateTime(eventInfo.Source.Value);
					break;

				case TypeCode.Empty:
					eventInfo.RenderedValue = "null";
					break;

				default:
					if (eventInfo.Source.Type == typeof(TimeSpan))
					{
						eventInfo.RenderedValue = YamlFormatter.FormatTimeSpan(eventInfo.Source.Value);
						break;
					}

					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "TypeCode.{0} is not supported.", typeCode));
			}

			base.Emit(eventInfo);
		}

		public override void Emit(MappingStartEventInfo eventInfo)
		{
			eventInfo.Style = MappingStyle.Flow;

			base.Emit(eventInfo);
		}

		public override void Emit(SequenceStartEventInfo eventInfo)
		{
			eventInfo.Style = SequenceStyle.Flow;

			base.Emit(eventInfo);
		}
	}
}