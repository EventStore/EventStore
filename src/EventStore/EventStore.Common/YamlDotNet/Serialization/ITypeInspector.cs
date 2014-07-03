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
using System.Collections.Generic;

namespace YamlDotNet.Serialization
{
	/// <summary>
	/// Provides access to the properties of a type.
	/// </summary>
	public interface ITypeInspector
	{
		/// <summary>
		/// Gets all properties of the specified type.
		/// </summary>
		/// <param name="type">The type whose properties are to be enumerated.</param>
		/// <param name="container">The actual object of type <paramref name="type"/> whose properties are to be enumerated. Can be null.</param>
		/// <returns></returns>
		IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container);

		/// <summary>
		/// Gets the property of the type with the specified name.
		/// </summary>
		/// <param name="type">The type whose properties are to be searched.</param>
		/// <param name="container">The actual object of type <paramref name="type"/> whose properties are to be searched. Can be null.</param>
		/// <param name="name">The name of the property.</param>
		/// <param name="ignoreUnmatched">
		/// Determines if an exception or null should be returned if <paramref name="name"/> can't be
		/// found in <paramref name="type"/>
		/// </param>
		/// <returns></returns>
		IPropertyDescriptor GetProperty(Type type, object container, string name, bool ignoreUnmatched);
	}
}
