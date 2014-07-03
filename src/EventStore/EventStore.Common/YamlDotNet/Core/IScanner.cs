//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2008, 2009, 2010, 2011 Antoine Aubry
    
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

using YamlDotNet.Core.Tokens;

namespace YamlDotNet.Core
{
	/// <summary>
	/// Defines the interface for a stand-alone YAML scanner that
	/// converts a sequence of characters into a sequence of YAML tokens.
	/// </summary>
	public interface IScanner
	{
		/// <summary>
		/// Gets the current position inside the input stream.
		/// </summary>
		/// <value>The current position.</value>
		Mark CurrentPosition
		{
			get;
		}

		/// <summary>
		/// Gets the current token.
		/// </summary>
		Token Current
		{
			get;
		}

		/// <summary>
		/// Moves to the next token.
		/// </summary>
		/// <returns></returns>
		bool MoveNext();

		/// <summary>
		/// Moves to the next token.
		/// </summary>
		/// <returns></returns>
		bool ParserMoveNext();

		/// <summary>
		/// Consumes the current token and increments the parsed token count
		/// </summary>
		void ConsumeCurrent();
	}
}