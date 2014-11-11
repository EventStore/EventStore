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
using EventStore.Rags.YamlDotNet.Core.Events;

namespace EventStore.Rags.YamlDotNet.Serialization.NodeTypeResolvers
{
	public sealed class TagNodeTypeResolver : INodeTypeResolver
	{
		private readonly IDictionary<string, Type> tagMappings;

		public TagNodeTypeResolver(IDictionary<string, Type> tagMappings)
		{
			if (tagMappings == null)
			{
				throw new ArgumentNullException("tagMappings");
			}

			this.tagMappings = tagMappings;
		}
		
		bool INodeTypeResolver.Resolve(NodeEvent nodeEvent, ref Type currentType)
		{
			Type predefinedType;
			if (!string.IsNullOrEmpty(nodeEvent.Tag) && tagMappings.TryGetValue(nodeEvent.Tag, out predefinedType))
			{
				currentType = predefinedType;
				return true;
			}
			return false;
		}
	}
}
