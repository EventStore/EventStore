// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public static class SerializerBuilderExtensions {
	public static SerializerBuilder SerializeJson(
		this SerializerBuilder self,
		JsonSerializerOptions options,
		Func<JsonSerializerBuilder, JsonSerializerBuilder> f) {

		var jsonBuilder = new JsonSerializerBuilder(self, options);
		f(jsonBuilder);

		return self;
	}
}
