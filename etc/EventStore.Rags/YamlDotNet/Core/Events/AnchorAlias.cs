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

using System.Globalization;

namespace EventStore.Rags.YamlDotNet.Core.Events
{
	/// <summary>
	/// Represents an alias event.
	/// </summary>
	public class AnchorAlias : ParsingEvent
	{
		/// <summary>
		/// Gets the event type, which allows for simpler type comparisons.
		/// </summary>
		internal override EventType Type {
			get {
				return EventType.Alias;
			}
		}
		
		private readonly string value;

		/// <summary>
		/// Gets the value of the alias.
		/// </summary>
		public string Value
		{
			get
			{
				return value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AnchorAlias"/> class.
		/// </summary>
		/// <param name="value">The value of the alias.</param>
		/// <param name="start">The start position of the event.</param>
		/// <param name="end">The end position of the event.</param>
		public AnchorAlias(string value, Mark start, Mark end)
			: base(start, end)
		{
			if(string.IsNullOrEmpty(value)) {
				throw new YamlException(start, end, "Anchor value must not be empty.");
			}

			if(!NodeEvent.anchorValidator.IsMatch(value)) {
				throw new YamlException(start, end, "Anchor value must contain alphanumerical characters only.");
			}
			
			this.value = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AnchorAlias"/> class.
		/// </summary>
		/// <param name="value">The value of the alias.</param>
		public AnchorAlias(string value)
			: this(value, Mark.Empty, Mark.Empty)
		{
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </returns>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Alias [value = {0}]", value);
		}

		/// <summary>
		/// Invokes run-time type specific Visit() method of the specified visitor.
		/// </summary>
		/// <param name="visitor">visitor, may not be null.</param>
		public override void Accept(IParsingEventVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}
