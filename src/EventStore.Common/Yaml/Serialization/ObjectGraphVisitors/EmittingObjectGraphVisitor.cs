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

namespace EventStore.Common.Yaml.Serialization.ObjectGraphVisitors
{
	public sealed class EmittingObjectGraphVisitor : IObjectGraphVisitor
	{
		private readonly IEventEmitter eventEmitter;

		public EmittingObjectGraphVisitor(IEventEmitter eventEmitter)
		{
			this.eventEmitter = eventEmitter;
		}

		bool IObjectGraphVisitor.Enter(IObjectDescriptor value)
		{
			return true;
		}

		bool IObjectGraphVisitor.EnterMapping(IObjectDescriptor key, IObjectDescriptor value)
		{
			return true;
		}

		bool IObjectGraphVisitor.EnterMapping(IPropertyDescriptor key, IObjectDescriptor value)
		{
			return true;
		}

		void IObjectGraphVisitor.VisitScalar(IObjectDescriptor scalar)
		{
			eventEmitter.Emit(new ScalarEventInfo(scalar));
		}

		void IObjectGraphVisitor.VisitMappingStart(IObjectDescriptor mapping, Type keyType, Type valueType)
		{
			eventEmitter.Emit(new MappingStartEventInfo(mapping));
		}

		void IObjectGraphVisitor.VisitMappingEnd(IObjectDescriptor mapping)
		{
			eventEmitter.Emit(new MappingEndEventInfo(mapping));
		}

		void IObjectGraphVisitor.VisitSequenceStart(IObjectDescriptor sequence, Type elementType)
		{
			eventEmitter.Emit(new SequenceStartEventInfo(sequence));
		}

		void IObjectGraphVisitor.VisitSequenceEnd(IObjectDescriptor sequence)
		{
			eventEmitter.Emit(new SequenceEndEventInfo(sequence));
		}
	}
}