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
using System.Linq;
using System.Reflection;

namespace YamlDotNet.Serialization.TypeInspectors
{
	/// <summary>
	/// Returns the properties of a type that are readable.
	/// </summary>
	public sealed class ReadablePropertiesTypeInspector : TypeInspectorSkeleton
	{
		private readonly ITypeResolver _typeResolver;

		public ReadablePropertiesTypeInspector(ITypeResolver typeResolver)
		{
			if (typeResolver == null)
			{
				throw new ArgumentNullException("typeResolver");
			}

			_typeResolver = typeResolver;
		}

		private static bool IsValidProperty(PropertyInfo property)
		{
			return property.CanRead
				&& property.GetGetMethod().GetParameters().Length == 0;
		}

		public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
		{
			return type
				.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.Where(IsValidProperty)
				.Select(p => (IPropertyDescriptor)new ReflectionPropertyDescriptor(p, _typeResolver));
		}

		private sealed class ReflectionPropertyDescriptor : IPropertyDescriptor
		{
			private readonly PropertyInfo _propertyInfo;
			private readonly ITypeResolver _typeResolver;

			public ReflectionPropertyDescriptor(PropertyInfo propertyInfo, ITypeResolver typeResolver)
			{
				_propertyInfo = propertyInfo;
				_typeResolver = typeResolver;
			}

			public string Name { get { return _propertyInfo.Name; } }
			public Type Type { get { return _propertyInfo.PropertyType; } }
			public Type TypeOverride { get; set; }
			public bool CanWrite { get { return _propertyInfo.CanWrite; } }

			public void Write(object target, object value)
			{
				_propertyInfo.SetValue(target, value, null);
			}

			public T GetCustomAttribute<T>() where T : Attribute
			{
				var attributes = _propertyInfo.GetCustomAttributes(typeof(T), true);
				return attributes.Length > 0
					? (T)attributes[0]
					: null;
			}

			public IObjectDescriptor Read(object target)
			{
				var propertyValue = _propertyInfo.GetValue(target, null);
				var actualType = TypeOverride ?? _typeResolver.Resolve(Type, propertyValue);
				return new ObjectDescriptor(propertyValue, actualType, Type);
			}
		}
	}
}
