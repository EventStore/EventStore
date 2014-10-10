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

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EventStore.Rags.YamlDotNet.Serialization.Utilities
{
	internal static class ReflectionUtility
	{
		/// <summary>
		/// Determines whether the specified type has a default constructor.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		/// 	<c>true</c> if the type has a default constructor; otherwise, <c>false</c>.
		/// </returns>
		public static bool HasDefaultConstructor(Type type)
		{
			return type.IsValueType || type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null;
		}

		public static Type GetImplementedGenericInterface(Type type, Type genericInterfaceType)
		{
			foreach (var interfacetype in GetImplementedInterfaces(type))
			{
				if (interfacetype.IsGenericType && interfacetype.GetGenericTypeDefinition() == genericInterfaceType)
				{
					return interfacetype;
				}
			}
			return null;
		}

		public static IEnumerable<Type> GetImplementedInterfaces(Type type)
		{
			if (type.IsInterface)
			{
				yield return type;
			}

			foreach (var implementedInterface in type.GetInterfaces())
			{
				yield return implementedInterface;
			}
		}

		public static MethodInfo GetMethod(Expression<Action> methodAccess)
		{
			var method = ((MethodCallExpression)methodAccess.Body).Method;
			if (method.IsGenericMethod)
			{
				method = method.GetGenericMethodDefinition();
			}
			return method;
		}

		public static MethodInfo GetMethod<T>(Expression<Action<T>> methodAccess)
		{
			var method = ((MethodCallExpression)methodAccess.Body).Method;
			if (method.IsGenericMethod)
			{
				method = method.GetGenericMethodDefinition();
			}
			return method;
		}

		private static readonly FieldInfo remoteStackTraceField = typeof(Exception)
				.GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);

		public static Exception Unwrap(this TargetInvocationException ex)
		{
			var result = ex.InnerException;
			if (remoteStackTraceField != null)
			{
				remoteStackTraceField.SetValue(ex.InnerException, ex.InnerException.StackTrace + "\r\n");
			}
			return result;
		}
	}

	public sealed class GenericStaticMethod
	{
		private readonly MethodInfo methodToCall;

		public GenericStaticMethod(Expression<Action> methodCall)
		{
			var callExpression = (MethodCallExpression)methodCall.Body;
			methodToCall = callExpression.Method.GetGenericMethodDefinition();
		}

		public object Invoke(Type[] genericArguments, params object[] arguments)
		{
			try
			{
				return methodToCall
					.MakeGenericMethod(genericArguments)
					.Invoke(null, arguments);
			}
			catch (TargetInvocationException ex)
			{
				throw ex.Unwrap();
			}
		}
	}

	public sealed class GenericInstanceMethod<TInstance>
	{
		private readonly MethodInfo methodToCall;

		public GenericInstanceMethod(Expression<Action<TInstance>> methodCall)
		{
			var callExpression = (MethodCallExpression)methodCall.Body;
			methodToCall = callExpression.Method.GetGenericMethodDefinition();
		}

		public object Invoke(Type[] genericArguments, TInstance instance, params  object[] arguments)
		{
			try
			{
				return methodToCall
					.MakeGenericMethod(genericArguments)
					.Invoke(instance, arguments);
			}
			catch (TargetInvocationException ex)
			{
				throw ex.Unwrap();
			}
		}
	}
}
