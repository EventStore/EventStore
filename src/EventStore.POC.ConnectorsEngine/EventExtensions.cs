// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine;

public static class EventExtensions {
	private const string JsonContent = "application/json";

	//qq where and how to deserialize event data for performance and memory management?
	// at the moment we are doing it for filtering and again in the sink
	public static JsonObject ToJson(this Event evt, ILogger logger) {

		JsonNode data = new JsonObject();
		if (evt.ContentType == JsonContent && !TryParseUtf8(evt.Data.Span, out data)) {
			logger.Warning("Failed to parse Data for event {stream}@{position}",
				evt.Stream, evt.EventNumber);
		}

		JsonNode metadata = new JsonObject();
		if (evt.ContentType == JsonContent && !TryParseUtf8(evt.Metadata.Span, out metadata)) {
			logger.Warning("Failed to parse MetaData for event {stream}@{position}",
				evt.Stream, evt.EventNumber);
		}

		var doc = new JsonObject {
			["eventId"] = evt.EventId,
			["created"] = evt.Created,
			["stream"] = evt.Stream,
			["eventNumber"] = evt.EventNumber,
			["eventType"] = evt.EventType,
			["contentType"] = evt.ContentType,
			["commitPosition"] = evt.CommitPosition,
			["preparePosition"] = evt.PreparePosition,
			["isRedacted"] = evt.IsRedacted,
			["data"] = data, //qq eventData, payload or maybe other more clearer name?
			["metadata"] = metadata
		};

		return doc;
	}

	public static bool TryParseUtf8(this ReadOnlySpan<byte> bytes, out JsonNode parsed) {
		parsed = new JsonObject();
		try {
			var eventDataReader = new Utf8JsonReader(bytes);
			parsed = JsonNode.Parse(ref eventDataReader) ?? new JsonObject();
			return true;
		} catch (Exception) {
			return false;
		}
	}
}
