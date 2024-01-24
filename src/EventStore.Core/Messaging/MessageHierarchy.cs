using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Serilog;

namespace EventStore.Core.Messaging;

public static class MessageHierarchyResolver {
	private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "MessageHierarchy");

	public static readonly Dictionary<Type, List<Type>> Descendants;
	public static readonly int[][] ParentsByTypeId;
	public static readonly int[][] DescendantsByTypeId;
	public static readonly Dictionary<Type, int[]> DescendantsByType;
	public static readonly Dictionary<Type, int> MsgTypeIdByType;
	public static readonly int MaxMsgTypeId;

	static MessageHierarchyResolver() {
		var sw = Stopwatch.StartNew();

		MsgTypeIdByType = new Dictionary<Type, int>();
		var descendants = new Dictionary<int, List<int>>();
		var parents = new Dictionary<int, List<int>>();
		var rootMsgType = typeof(Message);

		Descendants = new Dictionary<Type, List<Type>>();

		int msgTypeCount = 0;
		foreach (var msgType in EventStoreCore.MessageRegistry.MessageTypes) {
			msgTypeCount += 1;

			var msgTypeId = GetMsgTypeId(msgType);
			MsgTypeIdByType.Add(msgType, msgTypeId);
			parents.Add(msgTypeId, new List<int>());

			MaxMsgTypeId = Math.Max(msgTypeId, MaxMsgTypeId);
			//Log.WriteLine("Found {0} with MsgTypeId {1}", msgType.Name, msgTypeId);

			var type = msgType;
			while (true) {
				var typeId = GetMsgTypeId(type);
				parents[msgTypeId].Add(typeId);
				List<int> list;
				if (!descendants.TryGetValue(typeId, out list)) {
					list = new List<int>();
					descendants.Add(typeId, list);
				}

				list.Add(msgTypeId);

				List<Type> typeList;
				if (!Descendants.TryGetValue(type, out typeList)) {
					typeList = new List<Type>();
					Descendants.Add(type, typeList);
				}

				typeList.Add(msgType);

				if (type == rootMsgType)
					break;
				type = type.BaseType;
			}
		}

		if (msgTypeCount - 1 != MaxMsgTypeId) {
			var wrongTypes = from typeId in MsgTypeIdByType
				group typeId.Key by typeId.Value
				into g
				where g.Count() > 1
				select new {
					TypeId = g.Key,
					MsgTypes = g.ToArray()
				};

			foreach (var wrongType in wrongTypes) {
				Log.Fatal("MsgTypeId {typeId} is assigned to type: {messageTypes}",
					wrongType.TypeId,
					string.Join(", ", wrongType.MsgTypes.Select(x => x.Name)));
			}

			throw new Exception("Incorrect Message Type IDs setup.");
		}

		DescendantsByTypeId = new int[MaxMsgTypeId + 1][];
		ParentsByTypeId = new int[MaxMsgTypeId + 1][];
		for (int i = 0; i <= MaxMsgTypeId; ++i) {
			var list = descendants[i];
			DescendantsByTypeId[i] = new int[list.Count];
			for (int j = 0; j < list.Count; ++j) {
				DescendantsByTypeId[i][j] = list[j];
			}

			list = parents[i];
			ParentsByTypeId[i] = new int[list.Count];
			for (int j = 0; j < list.Count; ++j) {
				ParentsByTypeId[i][j] = list[j];
			}
		}

		DescendantsByType = new Dictionary<Type, int[]>();
		foreach (var typeIdMap in MsgTypeIdByType) {
			DescendantsByType.Add(typeIdMap.Key, DescendantsByTypeId[typeIdMap.Value]);
		}

		Log.Debug("MessageHierarchy initialization took {elapsed}.", sw.Elapsed);
	}

	private static int GetMsgTypeId(Type msgType) => EventStoreCore.MessageRegistry.Get(msgType).TypeId;
}

public readonly record struct MessageHierarchy {
	public IReadOnlyDictionary<Type, List<Type>> Descendants         { get; init; }
	public int[][]                               ParentsByTypeId     { get; init; }
	public int[][]                               DescendantsByTypeId { get; init; }
	public IReadOnlyDictionary<Type, int[]>      DescendantsByType   { get; init; }
	public IReadOnlyDictionary<Type, int>        MsgTypeIdByType     { get; init; }
	public int                                   MaxMsgTypeId        { get; init; }
}

[PublicAPI]
public class MessageHierarchyCatalog {
	public MessageHierarchyCatalog(List<Type> messageTypes) {
		foreach (var type in messageTypes) {
			Descendants[type] = ResolveDescendants(type, messageTypes);
			Ascendants[type]  = ResolveAscendants(type);
		}
	}

	Dictionary<Type, List<Type>> Descendants { get; } = new();
	Dictionary<Type, List<Type>> Ascendants  { get; } = new();

	public List<Type> DescendantsOf(Type type) => 
		Descendants.TryGetValue(type, out var descendants) ? descendants : [];

	public List<Type> AscendantsOf(Type type) => 
		Ascendants.TryGetValue(type, out var ascendants) ? ascendants : [];

	public List<Type> DescendantsOf<T>() where T : Message => DescendantsOf(typeof(T));
	public List<Type> AscendantsOf<T>()  where T : Message => AscendantsOf(typeof(T));
	
	
	static List<Type> ResolveDescendants(Type type, List<Type> types) {
		if (!typeof(Message).IsAssignableFrom(type))
			throw new ArgumentException($"Type {type!.FullName} is not a message.");
		
		return types
			.Where(p => type.IsAssignableFrom(p) && p != type && typeof(Message).IsAssignableFrom(p))
			.ToList();
	}

	static List<Type> ResolveAscendants(Type inputType) {
		if (!typeof(Message).IsAssignableFrom(inputType))
			throw new ArgumentException($"Type {inputType!.FullName} is not a message.");
		
		var ascendants = new List<Type>();
		var typesToProcess = new Stack<Type>();
		typesToProcess.Push(inputType);

		while (typesToProcess.Count > 0) {
			var type = typesToProcess.Pop();
			if (type == null || type == typeof(System.Object)) {
				continue;
			}

			// Include IEventStoreMessage in the ascendants list
			if (type == typeof(Message)) {
				ascendants.Insert(0, type);
				continue;
			}

			// Skip the input type, unless it's IEventStoreMessage
			if (type == inputType && type != typeof(Message)) {
				continue;
			}

			ascendants.Insert(0, type);

			// Add base type to the stack
			if (type.BaseType != null) {
				typesToProcess.Push(type.BaseType);
			}

			// Add interfaces to the stack
			foreach (var interfaceType in type.GetInterfaces()) {
				typesToProcess.Push(interfaceType);
			}
		}

		return ascendants;
		
		// var ascendants = new List<Type>();
		// var typesToProcess = new Stack<Type>();
		//
		// typesToProcess.Push(type);
		//
		// while (typesToProcess.Count > 0) {
		// 	type = typesToProcess.Pop();
		// 	if (type == null || type == typeof(System.Object)) {
		// 		continue;
		// 	}
		//
		// 	// Include IEventStoreMessage in the ascendants list
		// 	if (type == typeof(Message)) {
		// 		ascendants.Insert(0, type);
		// 		continue;
		// 	}
		//
		// 	ascendants.Insert(0, type);
		//
		// 	// Add base type to the stack
		// 	if (type.BaseType != null) typesToProcess.Push(type.BaseType);
		//
		// 	// Add interfaces to the stack
		// 	foreach (var interfaceType in type.GetInterfaces()) typesToProcess.Push(interfaceType);
		// }
		//
		// return ascendants;
		
		// var ascendants = new List<Type>();
		// while (type.BaseType is not null) {
		// 	if (typeof(Message).IsAssignableFrom(type.BaseType)) 
		// 		ascendants.Add(type.BaseType);
		// 	
		// 	type = type.BaseType;
		// }
		//
		// return ascendants;
		
		// var ascendants = new List<Type>();
		// while (type.BaseType != null) {
		// 	if (typeof(Message).IsAssignableFrom(type.BaseType) &&
		// 	    !(type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == typeof(Message<>)))
		// 	{
		// 		ascendants.Insert(0, type.BaseType);
		// 	}
		// 	type = type.BaseType;
		// }
		//
		// // Add interfaces
		// foreach (var interfaceType in type.GetInterfaces())
		// 	if (typeof(Message).IsAssignableFrom(interfaceType)) ascendants.Add(interfaceType);
		//
		// return ascendants;
	}
		
	public static MessageHierarchyCatalog Build(List<Type> messageTypes) => new(messageTypes);
}


