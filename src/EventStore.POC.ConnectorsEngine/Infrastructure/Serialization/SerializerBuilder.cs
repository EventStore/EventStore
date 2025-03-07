// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public class SerializerBuilder {
	private readonly Dictionary<Type, Func<Message, EventToWrite>> _serializers = new();
	private readonly Dictionary<string, Func<Event, Message?>> _deserializers = new();

	public SerializerBuilder RegisterSerializer<T>(string messageTypeName, int? version, Func<T, byte[]> serialize) {
		_serializers.Add(typeof(T), msg => {
			if (msg is not T typed)
				throw new InvalidOperationException(
					$"Unexpected error: serializer for {typeof(T).Name} was called with {msg.GetType().Name}");

			var serialized = new EventToWrite(
				eventId: Guid.NewGuid(),
				eventType: version is null
					? messageTypeName
					: $"{messageTypeName}.{version}",
				contentType: "application/json",
				data: serialize(typed),
				metadata: ReadOnlyMemory<byte>.Empty);
			return serialized;
		});
		return this;
	}

	public SerializerBuilder RegisterDeserializer(string messageTypeName, int? version, Func<Event, Message?> deserialize) {
		_deserializers.Add(
			version is null
				? messageTypeName
				: $"{messageTypeName}.{version}",
			evt => {
				var deserialized = deserialize(evt);
				return deserialized;
			});
		return this;
	}

	public Serializer Build() => new(_serializers, _deserializers);
}
