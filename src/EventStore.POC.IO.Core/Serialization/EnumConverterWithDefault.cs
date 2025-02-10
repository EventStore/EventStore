// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStore.POC.IO.Core.Serialization;

public class EnumConverterWithDefault<T> : JsonConverter<T> where T : struct, Enum {
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		var s = reader.GetString();
		if (!Enum.TryParse<T>(s, true, out var result) || !Enum.IsDefined(result))
			return default;
		return result;
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}
