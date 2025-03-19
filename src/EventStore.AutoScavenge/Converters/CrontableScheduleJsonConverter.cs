// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;
using System.Text.Json.Serialization;
using NCrontab;

namespace EventStore.AutoScavenge.Converters;

public class CrontableScheduleJsonConverter : JsonConverter<CrontabSchedule> {
	public override CrontabSchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		if (reader.TokenType != JsonTokenType.String)
			throw new JsonException("CRON expression can only be a string");

		try {
			return CrontabSchedule.Parse(reader.GetString());
		} catch (Exception ex) {
			throw new JsonException("Invalid CRON expression", ex);
		}
	}

	public override void Write(Utf8JsonWriter writer, CrontabSchedule value, JsonSerializerOptions options) {
		JsonSerializer.Serialize(writer, value.ToString(), options);
	}
}
