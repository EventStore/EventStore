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

namespace EventStore.Rags.YamlDotNet.Core
{
	/// <summary>
	/// Represents a location inside a file
	/// </summary>
	[Serializable]
	public class Mark
	{
		/// <summary>
		/// Gets a <see cref="Mark"/> with empty values.
		/// </summary>
		public static readonly Mark Empty = new Mark();

		/// <summary>
		/// Gets / sets the absolute offset in the file
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		/// Gets / sets the number of the line
		/// </summary>
		public int Line { get; private set; }

		/// <summary>
		/// Gets / sets the index of the column
		/// </summary>
		public int Column { get; private set; }

		public Mark()
		{
			Line = 1;
			Column = 1;
		}

		public Mark(int index, int line, int column)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index", "Index must be greater than or equal to zero.");
			}
			if (line < 1)
			{
				throw new ArgumentOutOfRangeException("line", "Line must be greater than or equal to 1.");
			}
			if (column < 1)
			{
				throw new ArgumentOutOfRangeException("column", "Column must be greater than or equal to 1.");
			}

			Index = index;
			Line = line;
			Column = column;
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String"/> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			return string.Format("Line: {0}, Col: {1}, Idx: {2}", Line, Column, Index);
		}
	}
}