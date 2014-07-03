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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization.ObjectGraphTraversalStrategies
{
	/// <summary>
	/// An implementation of <see cref="IObjectGraphTraversalStrategy"/> that traverses
	/// readable properties, collections and dictionaries.
	/// </summary>
	public class FullObjectGraphTraversalStrategy : IObjectGraphTraversalStrategy
	{
		protected readonly Serializer serializer;
		private readonly int maxRecursion;
		private readonly ITypeInspector typeDescriptor;
		private readonly ITypeResolver typeResolver;

		public FullObjectGraphTraversalStrategy(Serializer serializer, ITypeInspector typeDescriptor, ITypeResolver typeResolver, int maxRecursion)
		{
			if (maxRecursion <= 0)
			{
				throw new ArgumentOutOfRangeException("maxRecursion", maxRecursion, "maxRecursion must be greater than 1");
			}

			this.serializer = serializer;

			if (typeDescriptor == null)
			{
				throw new ArgumentNullException("typeDescriptor");
			}

			this.typeDescriptor = typeDescriptor;

			if (typeResolver == null)
			{
				throw new ArgumentNullException("typeResolver");
			}

			this.typeResolver = typeResolver;

			this.maxRecursion = maxRecursion;
		}

		void IObjectGraphTraversalStrategy.Traverse(IObjectDescriptor graph, IObjectGraphVisitor visitor)
		{
			Traverse(graph, visitor, 0);
		}

		protected virtual void Traverse(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
		{
			if (++currentDepth > maxRecursion)
			{
				throw new InvalidOperationException("Too much recursion when traversing the object graph");
			}

			if (!visitor.Enter(value))
			{
				return;
			}

			var typeCode = Type.GetTypeCode(value.Type);
			switch (typeCode)
			{
				case TypeCode.Boolean:
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
				case TypeCode.String:
				case TypeCode.Char:
				case TypeCode.DateTime:
					visitor.VisitScalar(value);
					break;

				case TypeCode.DBNull:
					visitor.VisitScalar(new ObjectDescriptor(null, typeof(object), typeof(object)));
					break;

				case TypeCode.Empty:
					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "TypeCode.{0} is not supported.", typeCode));

				default:
					if (value.Value == null || value.Type == typeof(TimeSpan))
					{
						visitor.VisitScalar(value);
						break;
					}

					var underlyingType = Nullable.GetUnderlyingType(value.Type);
					if (underlyingType != null)
					{
						// This is a nullable type, recursively handle it with its underlying type.
						// Note that if it contains null, the condition above already took care of it
						Traverse(new ObjectDescriptor(value.Value, underlyingType, value.Type), visitor, currentDepth);
					}
					else
					{
						TraverseObject(value, visitor, currentDepth);
					}
					break;
			}
		}

		protected virtual void TraverseObject(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
		{
			if (typeof(IDictionary).IsAssignableFrom(value.Type))
			{
				TraverseDictionary(value, visitor, currentDepth);
				return;
			}

			var dictionaryType = ReflectionUtility.GetImplementedGenericInterface(value.Type, typeof(IDictionary<,>));
			if (dictionaryType != null)
			{
				TraverseGenericDictionary(value, dictionaryType, visitor, currentDepth);
				return;
			}

			if (typeof(IEnumerable).IsAssignableFrom(value.Type))
			{
				TraverseList(value, visitor, currentDepth);
				return;
			}

			TraverseProperties(value, visitor, currentDepth);
		}

		protected virtual void TraverseDictionary(IObjectDescriptor dictionary, IObjectGraphVisitor visitor, int currentDepth)
		{
			visitor.VisitMappingStart(dictionary, typeof(object), typeof(object));

			foreach (DictionaryEntry entry in (IDictionary)dictionary.Value)
			{
				var key = GetObjectDescriptor(entry.Key, typeof(object));
				var value = GetObjectDescriptor(entry.Value, typeof(object));

				if (visitor.EnterMapping(key, value))
				{
					Traverse(key, visitor, currentDepth);
					Traverse(value, visitor, currentDepth);
				}
			}

			visitor.VisitMappingEnd(dictionary);
		}

		private void TraverseGenericDictionary(IObjectDescriptor dictionary, Type dictionaryType, IObjectGraphVisitor visitor, int currentDepth)
		{
			var entryTypes = dictionaryType.GetGenericArguments();

			// dictionaryType is IDictionary<TKey, TValue>
			visitor.VisitMappingStart(dictionary, entryTypes[0], entryTypes[1]);

			// Invoke TraverseGenericDictionaryHelper<,>
			traverseGenericDictionaryHelper.Invoke(entryTypes, this, dictionary.Value, visitor, currentDepth);

			visitor.VisitMappingEnd(dictionary);
		}

		private static readonly GenericInstanceMethod<FullObjectGraphTraversalStrategy> traverseGenericDictionaryHelper =
			new GenericInstanceMethod<FullObjectGraphTraversalStrategy>(s => s.TraverseGenericDictionaryHelper<int, int>(null, null, 0));

		private void TraverseGenericDictionaryHelper<TKey, TValue>(
			IDictionary<TKey, TValue> dictionary,
			IObjectGraphVisitor visitor, int currentDepth)
		{
			foreach (var entry in dictionary)
			{
				var key = GetObjectDescriptor(entry.Key, typeof(TKey));
				var value = GetObjectDescriptor(entry.Value, typeof(TValue));

				if (visitor.EnterMapping(key, value))
				{
					Traverse(key, visitor, currentDepth);
					Traverse(value, visitor, currentDepth);
				}
			}
		}

		private void TraverseList(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
		{
			var enumerableType = ReflectionUtility.GetImplementedGenericInterface(value.Type, typeof(IEnumerable<>));
			var itemType = enumerableType != null ? enumerableType.GetGenericArguments()[0] : typeof(object);

			visitor.VisitSequenceStart(value, itemType);

			foreach (var item in (IEnumerable)value.Value)
			{
				Traverse(GetObjectDescriptor(item, itemType), visitor, currentDepth);
			}

			visitor.VisitSequenceEnd(value);
		}

		protected virtual void TraverseProperties(IObjectDescriptor value, IObjectGraphVisitor visitor, int currentDepth)
		{
			visitor.VisitMappingStart(value, typeof(string), typeof(object));

			foreach (var propertyDescriptor in typeDescriptor.GetProperties(value.Type, value.Value))
			{
				var propertyValue = propertyDescriptor.Read(value.Value);

				if (visitor.EnterMapping(propertyDescriptor, propertyValue))
				{
					Traverse(new ObjectDescriptor(propertyDescriptor.Name, typeof(string), typeof(string)), visitor, currentDepth);
					Traverse(propertyValue, visitor, currentDepth);
				}
			}

			visitor.VisitMappingEnd(value);
		}

		private IObjectDescriptor GetObjectDescriptor(object value, Type staticType)
		{
			return new ObjectDescriptor(value, typeResolver.Resolve(staticType, value), staticType);
		}
	}
}