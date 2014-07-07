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
using EventStore.Common.Yaml.Core;
using EventStore.Common.Yaml.Core.Events;
using EventStore.Common.Yaml.Serialization.Utilities;

namespace EventStore.Common.Yaml.Serialization.ValueDeserializers
{
	public sealed class AliasValueDeserializer : IValueDeserializer
	{
		private readonly IValueDeserializer innerDeserializer;
		
		public AliasValueDeserializer(IValueDeserializer innerDeserializer)
		{
			if (innerDeserializer == null)
			{
				throw new ArgumentNullException ("innerDeserializer");			
			}
			
			this.innerDeserializer = innerDeserializer;
		}

		private sealed class AliasState : Dictionary<string, ValuePromise>, IPostDeserializationCallback
		{
			public void OnDeserialization()
			{
				foreach (var promise in Values)
				{
					if (!promise.HasValue)
					{
						throw new AnchorNotFoundException(promise.Alias.Start, promise.Alias.End, string.Format(
							"Anchor '{0}' not found",
							promise.Alias.Value
						));
					}
				}
			}
		}

		private sealed class ValuePromise : IValuePromise
		{
			public event Action<object> ValueAvailable;

			public bool HasValue { get; private set; }

			private object value;

			public readonly AnchorAlias Alias;

			public ValuePromise(AnchorAlias alias)
			{
				this.Alias = alias;
			}

			public ValuePromise(object value)
			{
				HasValue = true;
				this.value = value;
			}

			public object Value
			{
				get
				{
					if (!HasValue)
					{
						throw new InvalidOperationException("Value not set");
					}
					return value;
				}
				set
				{
					if (HasValue)
					{
						throw new InvalidOperationException("Value already set");
					}
					HasValue = true;
					this.value = value;

					if (ValueAvailable != null)
					{
						ValueAvailable(value);
					}
				}
			}
		}
		
		public object DeserializeValue (EventReader reader, Type expectedType, SerializerState state, IValueDeserializer nestedObjectDeserializer)
		{
			object value;
			var alias = reader.Allow<AnchorAlias>();
			if(alias != null)
			{
				var aliasState = state.Get<AliasState>();
				ValuePromise valuePromise;
				if(!aliasState.TryGetValue(alias.Value, out valuePromise))
				{
					valuePromise = new ValuePromise(alias);
					aliasState.Add(alias.Value, valuePromise);
				}

				return valuePromise.HasValue ? valuePromise.Value : valuePromise;
			}
			
			string anchor = null;
			
			var nodeEvent = reader.Peek<NodeEvent>();
			if(nodeEvent != null && !string.IsNullOrEmpty(nodeEvent.Anchor))
			{
				anchor = nodeEvent.Anchor;
			}
			
			value = innerDeserializer.DeserializeValue(reader, expectedType, state, nestedObjectDeserializer);
			
			if(anchor != null)
			{
				var aliasState = state.Get<AliasState>();

				ValuePromise valuePromise;
				if (!aliasState.TryGetValue(anchor, out valuePromise))
				{
					aliasState.Add(anchor, new ValuePromise(value));
				}
				else if (!valuePromise.HasValue)
				{
					valuePromise.Value = value;
				}
				else
				{
					throw new DuplicateAnchorException(nodeEvent.Start, nodeEvent.End, string.Format(
						"Anchor '{0}' already defined",
						anchor
					));
				}
			}
			
			return value;
		}
	}
}
