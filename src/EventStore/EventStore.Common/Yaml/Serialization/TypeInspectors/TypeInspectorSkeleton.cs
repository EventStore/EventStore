// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) 2013 Antoine Aubry
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace EventStore.Common.Yaml.Serialization.TypeInspectors
{
	public abstract class TypeInspectorSkeleton : ITypeInspector
	{
		public abstract IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container);

		public IPropertyDescriptor GetProperty(Type type, object container, string name, bool ignoreUnmatched)
		{
			var candidates = GetProperties(type, container)
				.Where(p => p.Name == name);

			using(var enumerator = candidates.GetEnumerator())
			{
				if(!enumerator.MoveNext())
				{
					if (ignoreUnmatched)
					{
						return null;
					}

					throw new SerializationException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Property '{0}' not found on type '{1}'.",
							name,
							type.FullName
						)
					);
				}
				
				var property = enumerator.Current;
				
				if(enumerator.MoveNext())
				{
					throw new SerializationException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Multiple properties with the name/alias '{0}' already exists on type '{1}', maybe you're misusing YamlAlias or maybe you are using the wrong naming convention? The matching properties are: {2}",
							name,
							type.FullName,
							string.Join(", ", candidates.Select(p => p.Name).ToArray())
						)
					);
				}

				return property;
			}
		}
	}
}
