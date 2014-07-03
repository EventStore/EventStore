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
using YamlDotNet.Core.Events;

namespace YamlDotNet.Serialization
{
	public interface INodeTypeResolver
	{
		/// <summary>
		/// Determines the type of the specified node.
		/// </summary>
		/// <param name="nodeEvent">The node to be deserialized.</param>
		/// <param name="currentType">The type that has been determined so far.</param>
		/// <returns>
		/// true if <paramref name="currentType"/> has been resolved completely;
		/// false if the next type <see cref="INodeTypeResolver"/> should be invoked.
		/// </returns>
		bool Resolve(NodeEvent nodeEvent, ref Type currentType);
	}
}
