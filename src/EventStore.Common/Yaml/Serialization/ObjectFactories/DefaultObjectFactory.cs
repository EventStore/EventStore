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

namespace EventStore.Common.Yaml.Serialization.ObjectFactories
{
	/// <summary>
	/// Creates objects using Activator.CreateInstance.
	/// </summary>
	public sealed class DefaultObjectFactory : IObjectFactory
	{
		private static readonly Dictionary<Type, Type> defaultInterfaceImplementations = new Dictionary<Type, Type>
		{
			{ typeof(IEnumerable<>), typeof(List<>) },
			{ typeof(ICollection<>), typeof(List<>) },
			{ typeof(IList<>), typeof(List<>) },
			{ typeof(IDictionary<,>), typeof(Dictionary<,>) },
		};

		public object Create(Type type)
		{
			if (type.IsInterface)
			{
				Type implementationType;
				if (defaultInterfaceImplementations.TryGetValue(type.GetGenericTypeDefinition(), out implementationType))
				{
					type = implementationType.MakeGenericType(type.GetGenericArguments());
				}
			}

			try
			{
				return Activator.CreateInstance(type);
			}
			catch (MissingMethodException err)
			{
				var message = string.Format("Failed to create an instance of type '{0}'.", type);
				throw new InvalidOperationException(message, err);
			}
		}
	}
}