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

namespace YamlDotNet.Core
{
	internal class StringLookAheadBuffer : ILookAheadBuffer
	{
		private readonly string value;

		public int Position { get; private set; }

		public StringLookAheadBuffer(string value)
		{
			this.value = value;
		}

		public int Length
		{
			get {
				return value.Length;
			}
		}

		public bool EndOfInput
		{
			get {
				return IsOutside(Position);
			}
		}

		public char Peek(int offset)
		{
			var index = Position + offset;
			return IsOutside(index) ? '\0' : value[index];
		}

		private bool IsOutside(int index)
		{
			return index >= value.Length;
		}

		public void Skip(int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length", "The length must be positive.");
			}
			Position += length;
		}
	}
}