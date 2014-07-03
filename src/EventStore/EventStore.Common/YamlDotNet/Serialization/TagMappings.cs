//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013 Antoine Aubry
    
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

namespace YamlDotNet.Serialization
{
	/// <summary>
	/// Contains mappings between tags and types.
	/// </summary>
	public sealed class TagMappings
	{
		private readonly IDictionary<string, Type> mappings;

		/// <summary>
		/// Initializes a new instance of the <see cref="TagMappings"/> class.
		/// </summary>
		public TagMappings()
		{
			mappings = new Dictionary<string, Type>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TagMappings"/> class.
		/// </summary>
		/// <param name="mappings">The mappings.</param>
		public TagMappings(IDictionary<string, Type> mappings)
		{
			this.mappings = new Dictionary<string, Type>(mappings);
		}

		/// <summary>
		/// Adds the specified tag.
		/// </summary>
		/// <param name="tag">The tag.</param>
		/// <param name="mapping">The mapping.</param>
		public void Add(string tag, Type mapping)
		{
			mappings.Add(tag, mapping);
		}

		/// <summary>
		/// Gets the mapping.
		/// </summary>
		/// <param name="tag">The tag.</param>
		/// <returns></returns>
		internal Type GetMapping(string tag)
		{
			Type mapping;
			if (mappings.TryGetValue(tag, out mapping))
			{
				return mapping;
			}
			else
			{
				return null;
			}
		}
	}
}