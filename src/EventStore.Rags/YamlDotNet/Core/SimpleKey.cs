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

namespace EventStore.Rags.YamlDotNet.Core
{
	internal class SimpleKey
	{
		private readonly Cursor cursor;

		public bool IsPossible { get; set; }

		public bool IsRequired { get; private set; }
		public int TokenNumber { get; private set; }
		public int Index { get { return cursor.Index; } }
		public int Line { get { return cursor.Line; } }
		public int LineOffset { get { return cursor.LineOffset; } }

		public Mark Mark { get { return cursor.Mark(); } }

		public SimpleKey()
		{
			cursor = new Cursor();
		}

		public SimpleKey(bool isPossible, bool isRequired, int tokenNumber, Cursor cursor)
		{
			IsPossible = isPossible;
			IsRequired = isRequired;
			TokenNumber = tokenNumber;
			this.cursor = new Cursor(cursor);
		}
	}
}