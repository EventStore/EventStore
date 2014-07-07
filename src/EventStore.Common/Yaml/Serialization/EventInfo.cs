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

using EventStore.Common.Yaml.Core;
using EventStore.Common.Yaml.Core.Events;

namespace EventStore.Common.Yaml.Serialization
{
	public abstract class EventInfo
	{
		public IObjectDescriptor Source { get; private set; }

		protected EventInfo(IObjectDescriptor source)
		{
			Source = source;
		}
	}

	public class AliasEventInfo : EventInfo
	{
		public AliasEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}

		public string Alias { get; set; }
	}

	public class ObjectEventInfo : EventInfo
	{
		protected ObjectEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}

		public string Anchor { get; set; }
		public string Tag { get; set; }
	}

	public sealed class ScalarEventInfo : ObjectEventInfo
	{
		public ScalarEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}

		public string RenderedValue { get; set; }
		public ScalarStyle Style { get; set; }
		public bool IsPlainImplicit { get; set; }
		public bool IsQuotedImplicit { get; set; }
	}

	public sealed class MappingStartEventInfo : ObjectEventInfo
	{
		public MappingStartEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}

		public bool IsImplicit { get; set; }
		public MappingStyle Style { get; set; }
	}

	public sealed class MappingEndEventInfo : EventInfo
	{
		public MappingEndEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}
	}

	public sealed class SequenceStartEventInfo : ObjectEventInfo
	{
		public SequenceStartEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}

		public bool IsImplicit { get; set; }
		public SequenceStyle Style { get; set; }
	}

	public sealed class SequenceEndEventInfo : EventInfo
	{
		public SequenceEndEventInfo(IObjectDescriptor source)
			: base(source)
		{
		}
	}
}