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

namespace EventStore.Common.Yaml.Core
{
	/// <summary>
	/// Specifies the version of the YAML language.
	/// </summary>
	public class Version
	{
		/// <summary>
		/// Gets the major version number.
		/// </summary>
		public int Major { get; private set; }

		/// <summary>
		/// Gets the minor version number.
		/// </summary>
		public int Minor { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Version"/> class.
		/// </summary>
		/// <param name="major">The the major version number.</param>
		/// <param name="minor">The the minor version number.</param>
		public Version(int major, int minor)
		{
			Major = major;
			Minor = minor;
		}

		/// <summary>
		/// Determines whether the specified System.Object is equal to the current System.Object.
		/// </summary>
		/// <param name="obj">The System.Object to compare with the current System.Object.</param>
		/// <returns>
		/// true if the specified System.Object is equal to the current System.Object; otherwise, false.
		/// </returns>
		public override bool Equals(object obj)
		{
			var that = obj as Version;
			return that != null && Major == that.Major && Minor == that.Minor;
		}

		/// <summary>
		/// Serves as a hash function for a particular type.
		/// </summary>
		/// <returns>
		/// A hash code for the current <see cref="T:System.Object"/>.
		/// </returns>
		public override int GetHashCode()
		{
			return Major.GetHashCode() ^ Minor.GetHashCode();
		}
	}
}