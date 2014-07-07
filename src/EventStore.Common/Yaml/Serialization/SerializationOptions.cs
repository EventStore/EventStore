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

namespace EventStore.Common.Yaml.Serialization
{
	/// <summary>
	/// Options that control the serialization process.
	/// </summary>
	[Flags]
	public enum SerializationOptions
	{
		/// <summary>
		/// Serializes using the default options
		/// </summary>
		None = 0,

		/// <summary>
		/// Ensures that it will be possible to deserialize the serialized objects.
		/// </summary>
		Roundtrip = 1,

		/// <summary>
		/// If this flag is specified, if the same object appears more than once in the
		/// serialization graph, it will be serialized each time instead of just once.
		/// </summary>
		/// <remarks>
		/// If the serialization graph contains circular references and this flag is set,
		/// a <see cref="StackOverflowException" /> will be thrown.
		/// If this flag is not set, there is a performance penalty because the entire
		/// object graph must be walked twice.
		/// </remarks>
		DisableAliases = 2,

		/// <summary>
		/// Forces every value to be serialized, even if it is the default value for that type.
		/// </summary>
		EmitDefaults = 4,

		/// <summary>
		/// Ensures that the result of the serialization is valid JSON.
		/// </summary>
		JsonCompatible = 8,

		/// <summary>
		/// Use the static type of values instead of their actual type.
		/// </summary>
		DefaultToStaticType = 16,
	}
}
