// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text;
using System.Text.Json;
using EventStore.POC.ConnectorsEngine;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.IO.Core;
using EventStore.POC.IO.Core.Serialization;

namespace Eventstore.POC.Tests;

public class SerializationTests {
	public enum State { None, Solid, Liquid, Gas, }
	public record TestEvent(State State) : Message;

	private JsonSerializerOptions _options;

	public SerializationTests() {
		_options = new JsonSerializerOptions() {
			PropertyNameCaseInsensitive = true,
			Converters = {
				new EnumConverterWithDefault<NodeState>(),
				new EnumConverterWithDefault<State>()
			}
		};
	}

	private static Event GenEvent(EventToWrite serialized) => new(
		eventId: serialized.EventId,
		created: DateTime.Now,
		stream: "stream",
		eventNumber: 3,
		eventType: serialized.EventType,
		contentType: serialized.ContentType,
		commitPosition: 4,
		preparePosition: 4,
		isRedacted: false,
		data: serialized.Data,
		metadata: serialized.Metadata);

	[Fact]
	public void can_round_trip() {
		var serializer = new SerializerBuilder()
			.SerializeJson(_options, b => b
				.RegisterSystemEvent<TestEvent>())
			.Build();

		var msg = new TestEvent(State.Liquid);

		// there
		var serialized = serializer.Serialize(msg);

		Assert.Equal("application/json", serialized.ContentType);
		Assert.Equal("$TestEvent.1", serialized.EventType);
		Assert.Equal(@$"{{""State"":""Liquid""}}", Encoding.UTF8.GetString(serialized.Data.Span));

		// and back again
		Assert.True(serializer.TryDeserialize(
			GenEvent(serialized),
			out var message,
			out var exception));

		var deserialized = Assert.IsType<TestEvent>(message);
		Assert.Equal(State.Liquid, deserialized.State);
	}

	[Fact]
	public void can_parse_unknown_enums() {
		var serializer = new SerializerBuilder()
			.SerializeJson(_options, b => b
				.RegisterSystemEvent<TestEvent>())
			.Build();

		var msg = new TestEvent((State)456);
		var serialized = serializer.Serialize(msg);

		Assert.True(serializer.TryDeserialize(
			GenEvent(serialized),
			out var message,
			out var exception));

		var deserialized = Assert.IsType<TestEvent>(message);
		Assert.Equal(State.None, deserialized.State);
	}
}
