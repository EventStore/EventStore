// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

namespace EventStore.POC.ConnectorsEngine;

public static class SerializerBuilderExtensions {
	public static JsonSerializerBuilder RegisterSystemEvent<T>(
		this JsonSerializerBuilder self,
		int? version = 1)
		where T : Message {

		return self.Register<T>(version: version, "$" + typeof(T).Name);
	}

	public static JsonSerializerBuilder ReadSystemEventWithContext<T>(
		this JsonSerializerBuilder self,
		INamingStrategy? namingStrategy = null,
		int? version = 1) {

		self.Inner.RegisterDeserializer(
			messageTypeName: "$" + typeof(T).Name,
			version: version,
			deserialize: evt => {
				var deserialized = JsonSerializer.Deserialize<T>(evt.Data.Span, self.Options);
				var id = namingStrategy is null ? evt.Stream : namingStrategy.IdFor(evt.Stream);
				return new ContextMessage<T>(evt.EventId, id, evt.EventNumber, deserialized!);
			});

		return self;
	}
}
