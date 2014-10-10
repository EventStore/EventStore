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

namespace EventStore.Rags.YamlDotNet.Serialization.ObjectGraphVisitors
{
	public sealed class AnchorAssigningObjectGraphVisitor : ChainedObjectGraphVisitor
	{
		private readonly IEventEmitter eventEmitter;
		private readonly IAliasProvider aliasProvider;
		private readonly HashSet<string> emittedAliases = new HashSet<string>();

		public AnchorAssigningObjectGraphVisitor(IObjectGraphVisitor nextVisitor, IEventEmitter eventEmitter, IAliasProvider aliasProvider)
			: base(nextVisitor)
		{
			this.eventEmitter = eventEmitter;
			this.aliasProvider = aliasProvider;
		}

		public override bool Enter(IObjectDescriptor value)
		{
			var alias = aliasProvider.GetAlias(value.Value);
			if (alias != null && !emittedAliases.Add(alias))
			{
				eventEmitter.Emit(new AliasEventInfo(value) { Alias = alias });
				return false;
			}

			return base.Enter(value);
		}

		public override void VisitMappingStart(IObjectDescriptor mapping, Type keyType, Type valueType)
		{
			eventEmitter.Emit(new MappingStartEventInfo(mapping) { Anchor = aliasProvider.GetAlias(mapping.Value) });
		}

		public override void VisitSequenceStart(IObjectDescriptor sequence, Type elementType)
		{
			eventEmitter.Emit(new SequenceStartEventInfo(sequence) { Anchor = aliasProvider.GetAlias(sequence.Value) });
		}

		public override void VisitScalar(IObjectDescriptor scalar)
		{
			eventEmitter.Emit(new ScalarEventInfo(scalar) { Anchor = aliasProvider.GetAlias(scalar.Value) });
		}
	}
}