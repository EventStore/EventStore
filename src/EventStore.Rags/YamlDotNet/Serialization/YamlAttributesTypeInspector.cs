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
using EventStore.Rags.YamlDotNet.Serialization.TypeInspectors;

namespace EventStore.Rags.YamlDotNet.Serialization
{
	/// <summary>
	/// Applies the Yaml* attributes to another <see cref="ITypeInspector"/>.
	/// </summary>
	public sealed class YamlAttributesTypeInspector : TypeInspectorSkeleton
	{
		private readonly ITypeInspector innerTypeDescriptor;

		public YamlAttributesTypeInspector(ITypeInspector innerTypeDescriptor)
		{
			this.innerTypeDescriptor = innerTypeDescriptor;
		}

		public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
		{
			return innerTypeDescriptor.GetProperties(type, container)
				.Where(p => p.GetCustomAttribute<YamlIgnoreAttribute>() == null)
				.Select(p =>
				{
					var descriptor = new PropertyDescriptor(p);

					var alias = p.GetCustomAttribute<YamlAliasAttribute>();
					if (alias != null)
					{
						descriptor.Name = alias.Alias;
					}

					var member = p.GetCustomAttribute<YamlMemberAttribute>();
					if (member != null)
					{
						if (member.SerializeAs != null)
						{
							descriptor.TypeOverride = member.SerializeAs;
						}
					}

					return (IPropertyDescriptor)descriptor;
				});
		}
	}
}
