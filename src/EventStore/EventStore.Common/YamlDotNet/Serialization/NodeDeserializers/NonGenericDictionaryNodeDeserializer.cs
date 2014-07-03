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
using System.Collections;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace YamlDotNet.Serialization.NodeDeserializers
{
	public sealed class NonGenericDictionaryNodeDeserializer : INodeDeserializer
	{
		private readonly IObjectFactory _objectFactory;

		public NonGenericDictionaryNodeDeserializer(IObjectFactory objectFactory)
		{
			_objectFactory = objectFactory;
		}

		bool INodeDeserializer.Deserialize(EventReader reader, Type expectedType, Func<EventReader, Type, object> nestedObjectDeserializer, out object value)
		{
			if(!typeof(IDictionary).IsAssignableFrom(expectedType))
			{
				value = false;
				return false;
			}

			reader.Expect<MappingStart>();

			var dictionary = (IDictionary)_objectFactory.Create(expectedType);
			while (!reader.Accept<MappingEnd>())
			{
				var key = nestedObjectDeserializer(reader, typeof(object));
				var keyPromise = key as IValuePromise;

				var keyValue = nestedObjectDeserializer(reader, typeof(object));
				var valuePromise = keyValue as IValuePromise;

				if (keyPromise == null)
				{
					if (valuePromise == null)
					{
						// Happy path: both key and value are known
						dictionary.Add(key, keyValue);
					}
					else
					{
						// Key is known, value is pending
						valuePromise.ValueAvailable += v => dictionary.Add(key, v);
					}
				}
				else
				{
					if (valuePromise == null)
					{
						// Key is pending, value is known
						keyPromise.ValueAvailable += v => dictionary.Add(v, keyValue);
					}
					else
					{
						// Both key and value are pending. We need to wait until both of them becom available.
						var hasFirstPart = false;

						keyPromise.ValueAvailable += v =>
						{
							if (hasFirstPart)
							{
								dictionary.Add(v, keyValue);
							}
							else
							{
								key = v;
								hasFirstPart = true;
							}
						};

						valuePromise.ValueAvailable += v =>
						{
							if (hasFirstPart)
							{
								dictionary.Add(key, v);
							}
							else
							{
								keyValue = v;
								hasFirstPart = true;
							}
						};
					}
				}
			}
			value = dictionary;

			reader.Expect<MappingEnd>();

			return true;
		}
	}
}
