// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public class Serializer : ISerializer {
	private readonly IReadOnlyDictionary<Type, Func<Message, EventToWrite>> _serializers;
	private readonly IReadOnlyDictionary<string, Func<Event, Message?>> _deserializers;

	public Serializer(
		IReadOnlyDictionary<Type, Func<Message, EventToWrite>> serializers,
		IReadOnlyDictionary<string, Func<Event, Message?>> deserializers) {

		_serializers = serializers;
		_deserializers = deserializers;
	}

	public EventToWrite Serialize(Message evt) {
		if (!_serializers.TryGetValue(evt.GetType(), out var serializer))
			throw new InvalidOperationException($"Missing serializer for {evt.GetType().FullName}");

		var serialized = serializer(evt);
		return serialized;
	}

	public bool TryDeserialize(Event evt, out Message? message, out Exception? exception) {
		message = null;
		exception = null;

		if (!_deserializers.TryGetValue(evt.EventType, out var deserialize)) {
			exception = new Exception("Missing deserializer");
			return false;
		}

		try {
			message = deserialize(evt);

			if (message is null) {
				exception = new Exception("Deserializer returned null");
				return false;
			}

			return true;
		} catch (Exception ex) {
			exception = ex;
			return false;
		}
	}
}
