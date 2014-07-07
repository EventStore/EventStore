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
using System.Collections.Generic;
using System.ComponentModel;

namespace EventStore.Common.Yaml.Serialization.ObjectGraphVisitors
{
	public sealed class DefaultExclusiveObjectGraphVisitor : ChainedObjectGraphVisitor
	{
		public DefaultExclusiveObjectGraphVisitor(IObjectGraphVisitor nextVisitor)
			: base(nextVisitor)
		{
		}

		private static object GetDefault(Type type)
		{
			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}

		private static readonly IEqualityComparer<object> _objectComparer = EqualityComparer<object>.Default;

		public override bool EnterMapping(IObjectDescriptor key, IObjectDescriptor value)
		{
			return !_objectComparer.Equals(value, GetDefault(value.Type))
			       && base.EnterMapping(key, value);
		}

		public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value)
		{
			var defaultValueAttribute = key.GetCustomAttribute<DefaultValueAttribute>();
			var defaultValue = defaultValueAttribute != null
				? defaultValueAttribute.Value
				: GetDefault(key.Type);

			return !_objectComparer.Equals(value.Value, defaultValue)
				   && base.EnterMapping(key, value);
		}
	}
}