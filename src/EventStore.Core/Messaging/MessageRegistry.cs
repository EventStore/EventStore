using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.Core.Scanning;
using JetBrains.Annotations;

namespace EventStore.Core.Messaging;

/// <summary>
/// Global Accessor for Core Infrastructure Components
/// </summary>
[PublicAPI]
public static class EventStoreCore {
	public static IReadOnlyList<Type>  MessageTypes     { get; private set; }
	public static MessageRegistry      MessageRegistry  { get; private set; }
	public static MessageHierarchy MessageHierarchy { get; private set; }
	
	static bool IsInitialized { get; set; }

	public static void Initialize() {
		if (IsInitialized)
			throw new Exception("Already initialized.");
		
		var catalog = new DependencyContextAssemblyCatalog();

		var messageTypes = MessagesAssemblyScanner
			.FindMessagesInAssemblies(catalog.GetAssemblies())
			.Select(x => x.MessageType)
			.ToList();
		
		Initialize(messageTypes);
	}
	
	public static void Initialize(IReadOnlyList<Type> messageTypes) {
		if (IsInitialized)
			throw new Exception("Already initialized.");
		
		MessageTypes = messageTypes;
		
		MessageRegistry = MessageRegistry.InitializeShared(new MessageRegistry(MessageTypes));
		
		MessageHierarchy = new MessageHierarchy {
			Descendants         = MessageHierarchyResolver.Descendants,
			ParentsByTypeId     = MessageHierarchyResolver.ParentsByTypeId,
			DescendantsByTypeId = MessageHierarchyResolver.DescendantsByTypeId,
			DescendantsByType   = MessageHierarchyResolver.DescendantsByType,
			MsgTypeIdByType     = MessageHierarchyResolver.MsgTypeIdByType,
			MaxMsgTypeId        = MessageHierarchyResolver.MaxMsgTypeId
		};
		
		IsInitialized = true;
	}
}

static class MessageRegistrationInfo<T> where T : Message {
	// ReSharper disable once StaticMemberInGenericType
	private static readonly MessageRegistrationInfo _info;

	static MessageRegistrationInfo() => _info = EventStoreCore.MessageRegistry.Get<T>();

	public static void Initialize() { } // elegant way to trigger static ctor

	public static Type   ClrType => _info.ClrType;
	public static int    TypeId  => _info.TypeId;
	public static string Label   => _info.Label;
}

public readonly record struct MessageLabelMap(string RegexPattern, string Label) {
	public static readonly MessageLabelMap Empty = new("", "");
}

[PublicAPI]
public readonly record struct MessageRegistrationInfo(Type ClrType, string Label, int TypeId) {
	public static readonly MessageRegistrationInfo Empty = new(null!, "", -1);
}

[PublicAPI]
public class MessageRegistry {
	public static MessageRegistry Shared { get; private set; } = null!;

	static bool IsInitialized { get; set; }
	
	public static MessageRegistry InitializeShared(MessageRegistry registry) {
		if (IsInitialized)
			return Shared;
		
		Shared = registry;
		IsInitialized = true;
		
		return registry;
	}

	private int _counter = -1;

	public MessageRegistry(IReadOnlyList<Type> messageTypes) {
		MessageTypes = messageTypes;

		foreach (var type in messageTypes) {
			var (label, group) = GenerateDefaultLabel(type);
			Register(type, label);
		}
	}
	
	Dictionary<Type, MessageRegistrationInfo> Registrations { get; } = new();
	
	public IReadOnlyList<Type> MessageTypes { get; }
	
	public MessageRegistrationInfo Register([NotNull] Type type, [NotNull] string label) {
		if (type == null) 
			throw new ArgumentNullException(nameof(type));

		if (string.IsNullOrWhiteSpace(label)) 
			throw new ArgumentException("Value cannot be null or whitespace.", nameof(label));
		
		var entry = new MessageRegistrationInfo(type, label, NextSequenceId());
		
		Registrations.Add(type, entry);

		return entry;
	}

	public MessageRegistrationInfo Register<T>(string label) where T : Message =>
		Register(typeof(T), label);

	public bool TryGet(Type type, out MessageRegistrationInfo registration) =>
		Registrations.TryGetValue(type, out registration);

	public bool TryGet<T>(out MessageRegistrationInfo registration) where T : Message =>
		TryGet(typeof(T), out registration);

	public MessageRegistrationInfo Get(Type type) {
		if (TryGet(type, out var info))
			return info;

		throw new Exception($"Info not found for message {type.FullName}");
	}

	public MessageRegistrationInfo Get<T>() => Get(typeof(T));

	public (string Label, string Group) GenerateDefaultLabel([NotNull] Type type) {
		if (type == null) 
			throw new ArgumentNullException(nameof(type));
		
		if (!type.IsNestedPublic)
			return (type.Name, "");

		var group = type.DeclaringType!.Name.Replace("Message", "");
		return ($"{group}.{type.Name}", group);
	}

	public (string Label, string Group) GenerateDefaultLabel<T>() where T : Message =>
		GenerateDefaultLabel(typeof(T));

	public List<(Type MessageType, string OriginalLabel, string Label, int TypeId)> Relabel([NotNull] string regexPattern, [NotNull] string label) {
		if (string.IsNullOrWhiteSpace(regexPattern))
			throw new ArgumentException("Value cannot be null or whitespace.", nameof(regexPattern));
		
		if (string.IsNullOrWhiteSpace(label)) 
			throw new ArgumentException("Value cannot be null or whitespace.", nameof(label));
		
		var pattern = $"^({Regex.Escape(regexPattern)})";
		var regex = new Regex(pattern, RegexOptions.Compiled);
		var matches = MessageTypes.Where(type => regex.IsMatch(type.FullName!)).ToList();

		var results = new List<(Type MessageType, string OriginalLabel, string Label, int TypeId)>();

		foreach (var type in matches) {
			if (Registrations.TryGetValue(type, out var registration)) {
				Registrations[type] = registration with { Label = label };
				results.Add((type, registration.Label, label, registration.TypeId));
			} else {
				Registrations.Add(type, registration with { Label = label });
				results.Add((type, "", label, registration.TypeId));
			}
		}

		return results;
	}

	public List<(Type MessageType, string OriginalLabel, string Label, int TypeId)> Relabel(MessageLabelMap[] maps) => 
		maps.SelectMany(x => Relabel(x.RegexPattern, x.Label)).ToList();
	
	int NextSequenceId() => _counter++;
}
