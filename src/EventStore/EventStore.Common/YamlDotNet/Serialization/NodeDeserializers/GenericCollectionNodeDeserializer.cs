// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) 2013 aaubry
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
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization.NodeDeserializers
{
	public sealed class GenericCollectionNodeDeserializer : INodeDeserializer
	{
		private readonly IObjectFactory _objectFactory;

		public GenericCollectionNodeDeserializer(IObjectFactory objectFactory)
		{
			_objectFactory = objectFactory;
		}

		bool INodeDeserializer.Deserialize(EventReader reader, Type expectedType, Func<EventReader, Type, object> nestedObjectDeserializer, out object value)
		{
			var iCollection = ReflectionUtility.GetImplementedGenericInterface(expectedType, typeof(ICollection<>));
			if (iCollection == null)
			{
				value = false;
				return false;
			}

			value = _objectFactory.Create(expectedType);
			_deserializeHelper.Invoke(iCollection.GetGenericArguments(), reader, expectedType, nestedObjectDeserializer, value);

			return true;
		}

		private static readonly GenericStaticMethod _deserializeHelper = new GenericStaticMethod(() => DeserializeHelper<object>(null, null, null, null));

		internal static void DeserializeHelper<TItem>(EventReader reader, Type expectedType, Func<EventReader, Type, object> nestedObjectDeserializer, ICollection<TItem> result)
		{
			var list = result as IList<TItem>;

			reader.Expect<SequenceStart>();
			while (!reader.Accept<SequenceEnd>())
			{
				var current = reader.Parser.Current;

				var value = nestedObjectDeserializer(reader, typeof(TItem));
				var promise = value as IValuePromise;
				if (promise == null)
				{
					result.Add(TypeConverter.ChangeType<TItem>(value));
				}
				else if(list != null)
				{
					var index = list.Count;
					result.Add(default(TItem));
					promise.ValueAvailable += v => list[index] = TypeConverter.ChangeType<TItem>(v);
				}
				else
				{
					throw new ForwardAnchorNotSupportedException(
						current.Start,
						current.End,
						"Forward alias references are not allowed because this type does not implement IList<>"
					);
				}
			}
			reader.Expect<SequenceEnd>();
		}
	}
}
