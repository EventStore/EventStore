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
using System.Linq;
using EventStore.Rags.YamlDotNet.Serialization.Utilities;

namespace EventStore.Rags.YamlDotNet.Serialization.ObjectGraphTraversalStrategies
{
	/// <summary>
	/// An implementation of <see cref="IObjectGraphTraversalStrategy"/> that traverses
	/// properties that are read/write, collections and dictionaries, while ensuring that
	/// the graph can be regenerated from the resulting document.
	/// </summary>
	public class RoundtripObjectGraphTraversalStrategy : FullObjectGraphTraversalStrategy
	{
		public RoundtripObjectGraphTraversalStrategy(Serializer serializer, ITypeInspector typeDescriptor, ITypeResolver typeResolver, int maxRecursion)
			: base(serializer, typeDescriptor, typeResolver, maxRecursion)
		{
		}

		protected override void TraverseProperties(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
		{
			if (!ReflectionUtility.HasDefaultConstructor(value.Type) && !serializer.Converters.Any(c => c.Accepts(value.Type)))
			{
				throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Type '{0}' cannot be deserialized because it does not have a default constructor or a type converter.", value.Type));
			}

			base.TraverseProperties(value, visitor, currentDepth);
		}
	}
}