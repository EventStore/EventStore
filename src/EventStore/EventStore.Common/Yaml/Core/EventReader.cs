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

using System.IO;
using System.Globalization;
using EventStore.Common.Yaml.Core.Events;

namespace EventStore.Common.Yaml.Core
{
	/// <summary>
	/// Reads events from a sequence of <see cref="ParsingEvent" />.
	/// </summary>
	public class EventReader
	{
		private readonly IParser parser;
		private bool endOfStream;

		/// <summary>
		/// Initializes a new instance of the <see cref="EventReader"/> class.
		/// </summary>
		/// <param name="parser">The parser that provides the events.</param>
		public EventReader(IParser parser)
		{
			this.parser = parser;
			MoveNext();
		}

		/// <summary>
		/// Gets the underlying parser.
		/// </summary>
		/// <value>The parser.</value>
		public IParser Parser
		{
			get
			{
				return parser;
			}
		}

		/// <summary>
		/// Ensures that the current event is of the specified type, returns it and moves to the next event.
		/// </summary>
		/// <typeparam name="T">Type of the <see cref="ParsingEvent"/>.</typeparam>
		/// <returns>Returns the current event.</returns>
		/// <exception cref="YamlException">If the current event is not of the specified type.</exception>
		public T Expect<T>() where T : ParsingEvent
		{
			var expectedEvent = Allow<T>();
			if (expectedEvent == null)
			{
				// TODO: Throw a better exception
				var @event = parser.Current;
				throw new YamlException(@event.Start, @event.End, string.Format(CultureInfo.InvariantCulture,
				        "Expected '{0}', got '{1}' (at {2}).", typeof(T).Name, @event.GetType().Name, @event.Start));
			}
			return expectedEvent;
		}

		/// <summary>
		/// Checks whether the current event is of the specified type.
		/// </summary>
		/// <typeparam name="T">Type of the event.</typeparam>
		/// <returns>Returns true if the current event is of type <typeparamref name="T"/>. Otherwise returns false.</returns>
		public bool Accept<T>() where T : ParsingEvent
		{
			ThrowIfAtEndOfStream();
			return parser.Current is T;
		}

		private void ThrowIfAtEndOfStream()
		{
			if (endOfStream)
			{
				throw new EndOfStreamException();
			}
		}

		/// <summary>
		/// Checks whether the current event is of the specified type.
		/// If the event is of the specified type, returns it and moves to the next event.
		/// Otherwise retruns null.
		/// </summary>
		/// <typeparam name="T">Type of the <see cref="ParsingEvent"/>.</typeparam>
		/// <returns>Returns the current event if it is of type T; otherwise returns null.</returns>
		public T Allow<T>() where T : ParsingEvent
		{
			if (!Accept<T>())
			{
				return null;
			}
			var @event = (T) parser.Current;
			MoveNext();
			return @event;
		}

		/// <summary>
		/// Gets the next event without consuming it.
		/// </summary>
		/// <typeparam name="T">Type of the <see cref="ParsingEvent"/>.</typeparam>
		/// <returns>Returns the current event if it is of type T; otherwise returns null.</returns>
		public T Peek<T>() where T : ParsingEvent
		{
			if (!Accept<T>())
			{
				return null;
			}
			return (T) parser.Current;
		}

		/// <summary>
		/// Skips the current event and any nested event.
		/// </summary>
		public void SkipThisAndNestedEvents()
		{
			var depth = 0;
			do
			{
				depth += Peek<ParsingEvent>().NestingIncrease;
				MoveNext();
			}
			while(depth > 0);
		}

		private void MoveNext()
		{
			endOfStream = !parser.MoveNext();
		}
	}
}