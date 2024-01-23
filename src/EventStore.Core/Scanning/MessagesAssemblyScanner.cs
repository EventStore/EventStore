#nullable enable

using EventStore.Core.Messaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace EventStore.Core.Scanning;

/// <summary>
/// Class that can be used to find all the messages from a collection of types.
/// </summary>
[PublicAPI]
public class MessagesAssemblyScanner : IEnumerable<MessagesAssemblyScanner.AssemblyScanResult> {
	private readonly IEnumerable<Type> _types;

	/// <summary>
	/// Creates a scanner that works on a sequence of types.
	/// </summary>
	public MessagesAssemblyScanner(IEnumerable<Type> types) => _types = types;

	/// <summary>
	/// Finds all the messages in the specified assembly.
	/// </summary>
	/// <param name="assembly">The assembly to scan</param>
	/// <param name="includeInternalTypes">Whether to include internal messages in the search as well as public messages. The default is false.</param>
	public static MessagesAssemblyScanner FindMessagesInAssembly(Assembly assembly, bool includeInternalTypes = true) => 
		new(includeInternalTypes ? assembly.GetTypes() : assembly.GetExportedTypes());

	/// <summary>
	/// Finds all the messages in the specified assemblies.
	/// </summary>
	/// <param name="assemblies">The assemblies to scan</param>
	/// <param name="includeInternalTypes">Whether to include internal messages as well as public messages. The default is false.</param>
	public static MessagesAssemblyScanner FindMessagesInAssemblies(IEnumerable<Assembly> assemblies, bool includeInternalTypes = true) {
		var types = assemblies.SelectMany(x => includeInternalTypes ? x.GetTypes() : x.GetExportedTypes()).Distinct();
		return new MessagesAssemblyScanner(types);
	}

	/// <summary>
	/// Finds all the messages in the assembly containing the specified type.
	/// </summary>
	public static MessagesAssemblyScanner FindMessagesInAssemblyContaining<T>() => 
		FindMessagesInAssembly(typeof(T).Assembly);

	/// <summary>
	/// Finds all the messages in the assembly containing the specified type.
	/// </summary>
	public static MessagesAssemblyScanner FindMessagesInAssemblyContaining(Type type) =>
		FindMessagesInAssembly(type.Assembly);

	private IEnumerable<AssemblyScanResult> Execute() {
		var openGenericType = typeof(Message<>);
		var baseType = typeof(Message);

		var query = from type in _types
			where !type.IsAbstract && type.IsAssignableTo(baseType)
			select new AssemblyScanResult(type);

		return query;
	}
	
	public static IEnumerable<AssemblyScanResult> FindMessagesInAssemblies(IEnumerable<Assembly> assemblies, Func<AssemblyScanResult, bool>? filter) =>
		FindMessagesInAssemblies(assemblies, includeInternalTypes: true)
			.Where(x => x.MessageType.GetTypeInfo().IsAssignableTo(typeof(Message)) && (filter?.Invoke(x) ?? true));

	public static IEnumerable<AssemblyScanResult> FindMessagesInAssemblies(Func<AssemblyScanResult, bool>? filter = null) =>
		FindMessagesInAssemblies(DependencyContextAssemblyCatalog.Default.GetAssemblies(), filter);
	
	public IEnumerator<AssemblyScanResult> GetEnumerator() => Execute().GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	
	public readonly record struct AssemblyScanResult(Type MessageType);
}
