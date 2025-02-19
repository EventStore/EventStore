// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStore.AutoScavenge.Converters;

public class EventJsonConverter : JsonConverter<IEvent> {
	public override IEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		throw new NotImplementedException();
	}

	public override void Write(Utf8JsonWriter writer, IEvent value, JsonSerializerOptions options) {
		JsonSerializer.Serialize(writer, value, value.GetType(), options);
	}
}
