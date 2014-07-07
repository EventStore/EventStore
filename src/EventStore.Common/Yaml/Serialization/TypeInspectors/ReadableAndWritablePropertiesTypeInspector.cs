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

namespace EventStore.Common.Yaml.Serialization.TypeInspectors
{
	/// <summary>
	/// Returns the properties of a type that are both readable and writable.
	/// </summary>
	public sealed class ReadableAndWritablePropertiesTypeInspector : TypeInspectorSkeleton
	{
		private readonly ITypeInspector _innerTypeDescriptor;

		public ReadableAndWritablePropertiesTypeInspector(ITypeInspector innerTypeDescriptor)
		{
			_innerTypeDescriptor = innerTypeDescriptor;
		}
	
		public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
		{
			return _innerTypeDescriptor.GetProperties(type, container)
				.Where(p => p.CanWrite);
		}
	}
}
