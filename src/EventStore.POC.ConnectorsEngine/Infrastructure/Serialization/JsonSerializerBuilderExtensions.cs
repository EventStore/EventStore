// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public static class JsonSerializerBuilderExtensions {
	public static JsonSerializerBuilder Register<T>(
		this JsonSerializerBuilder self,
		int? version = 1,
		string? messageTypeName = null)
		where T : Message {

		self.Inner.RegisterSerializer<T>(
			messageTypeName: messageTypeName ?? typeof(T).Name,
			version: version,
			serialize: msg => {
				var serialized = JsonSerializer.SerializeToUtf8Bytes(msg, self.Options);
				return serialized;
			});

		self.Inner.RegisterDeserializer(
			messageTypeName: messageTypeName ?? typeof(T).Name,
			version: version,
			deserialize: evt => {
				var deserialized = JsonSerializer.Deserialize<T>(evt.Data.Span, self.Options);
				return deserialized;
			});

		return self;
	}
}
