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
using System.Globalization;

namespace EventStore.Common.Yaml.Serialization.ObjectGraphVisitors
{
	public sealed class AnchorAssigner : IObjectGraphVisitor, IAliasProvider
	{
		private class AnchorAssignment
		{
			public string Anchor;
		}

		private readonly IDictionary<object, AnchorAssignment> assignments = new Dictionary<object, AnchorAssignment>();
		private uint nextId;

		bool IObjectGraphVisitor.Enter(IObjectDescriptor value)
		{
			// Do not assign anchors to basic types
			if (value.Value == null || Type.GetTypeCode(value.Type) != TypeCode.Object)
			{
				return false;
			}

			AnchorAssignment assignment;
			if (assignments.TryGetValue(value.Value, out assignment))
			{
				if (assignment.Anchor == null)
				{
					assignment.Anchor = "o" + nextId.ToString(CultureInfo.InvariantCulture);
					++nextId;
				}
				return false;
			}
			else
			{
				assignments.Add(value.Value, new AnchorAssignment());
				return true;
			}
		}

		bool IObjectGraphVisitor.EnterMapping(IObjectDescriptor key, IObjectDescriptor value)
		{
			return true;
		}

		bool IObjectGraphVisitor.EnterMapping(IPropertyDescriptor key, IObjectDescriptor value)
		{
			return true;
		}

		void IObjectGraphVisitor.VisitScalar(IObjectDescriptor scalar) { }
		void IObjectGraphVisitor.VisitMappingStart(IObjectDescriptor mapping, Type keyType, Type valueType) { }
		void IObjectGraphVisitor.VisitMappingEnd(IObjectDescriptor mapping) { }
		void IObjectGraphVisitor.VisitSequenceStart(IObjectDescriptor sequence, Type elementType) { }
		void IObjectGraphVisitor.VisitSequenceEnd(IObjectDescriptor sequence) { }

		string IAliasProvider.GetAlias(object target)
		{
			AnchorAssignment assignment;
			if (target != null && assignments.TryGetValue(target, out assignment))
			{
				return assignment.Anchor;
			}
			return null;
		}
	}
}