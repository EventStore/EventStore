// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public class JsonSerializerBuilder {
	public SerializerBuilder Inner { get; }

	public JsonSerializerOptions Options { get; }

	public JsonSerializerBuilder(SerializerBuilder inner, JsonSerializerOptions options) {
		Inner = inner;
		Options = options;
	}
}
