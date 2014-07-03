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

namespace YamlDotNet.Core
{
	internal class Cursor
	{
		public int Index { get; set; }
		public int Line { get; set; }
		public int LineOffset { get; set; }

		public Cursor()
		{
			Line = 1;
		}

		public Cursor(Cursor cursor)
		{
			Index = cursor.Index;
			Line = cursor.Line;
			LineOffset = cursor.LineOffset;
		}

		public Mark Mark()
		{
			return new Mark(Index, Line, LineOffset + 1);
		}

		public void Skip()
		{
			Index++;
			LineOffset++;
		}
		
		public void SkipLineByOffset(int offset)
		{
			Index += offset;
			Line++;
			LineOffset = 0;
		}

		public void ForceSkipLineAfterNonBreak()
		{
			if (LineOffset != 0)
			{
				Line++;
				LineOffset = 0;
			}
		}
	}
}
