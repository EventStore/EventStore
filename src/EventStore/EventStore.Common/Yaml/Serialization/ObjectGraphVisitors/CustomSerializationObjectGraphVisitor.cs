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

using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Yaml.Core;

namespace EventStore.Common.Yaml.Serialization.ObjectGraphVisitors
{
	public sealed class CustomSerializationObjectGraphVisitor : ChainedObjectGraphVisitor
	{
		private readonly IEmitter emitter;
		private readonly IEnumerable<IYamlTypeConverter> typeConverters;

		public CustomSerializationObjectGraphVisitor(IEmitter emitter, IObjectGraphVisitor nextVisitor, IEnumerable<IYamlTypeConverter> typeConverters)
			: base(nextVisitor)
		{
			this.emitter = emitter;
			this.typeConverters = typeConverters != null
				? typeConverters.ToList()
				: Enumerable.Empty<IYamlTypeConverter>();
		}

		public override bool Enter(IObjectDescriptor value)
		{
			var typeConverter = typeConverters.FirstOrDefault(t => t.Accepts(value.Type));
			if (typeConverter != null)
			{
				typeConverter.WriteYaml(emitter, value.Value, value.Type);
				return false;
			}

			var serializable = value as IYamlSerializable;
			if (serializable != null)
			{
				serializable.WriteYaml(emitter);
				return false;
			}

			return base.Enter(value);
		}
	}
}
