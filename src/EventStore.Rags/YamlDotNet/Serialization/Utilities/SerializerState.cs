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
using System.Linq;

namespace EventStore.Rags.YamlDotNet.Serialization.Utilities
{
	/// <summary>
	/// A generic container that is preserved during the entire deserialization process.
	/// Any disposable object added to this collecion will be disposed when this object is disposed.
	/// </summary>
	public sealed class SerializerState : IDisposable
	{
		private readonly IDictionary<Type, object> items = new Dictionary<Type, object>();
		
		public T Get<T>()
			where T : class, new()
		{
			object value;
			if(!items.TryGetValue(typeof(T), out value))
			{
				value = new T();
				items.Add(typeof(T), value);
			}
			return (T)value;
		}

		/// <summary>
		/// Invokes <see cref="IPostDeserializationCallback.OnDeserialization" /> on all
		/// objects added to this collection that implement <see cref="IPostDeserializationCallback" />.
		/// </summary>
		public void OnDeserialization()
		{
			foreach (var callback in items.Values.OfType<IPostDeserializationCallback>())
			{
				callback.OnDeserialization();
			}
		}

		public void Dispose()
		{
			foreach (var disposable in items.Values.OfType<IDisposable>())
			{
				disposable.Dispose();
			}
		}
	}
}
