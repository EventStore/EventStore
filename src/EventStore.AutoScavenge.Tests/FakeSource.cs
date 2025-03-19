// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text;
using System.Text.Json;
using EventStore.AutoScavenge.Converters;
using EventStore.AutoScavenge.Sources;

namespace EventStore.AutoScavenge.Tests;

public class FakeSource : ISource {
	private readonly FakeClient _fakeClient;
	private readonly ClientSource _clientSource;

	public FakeSource() {
		_fakeClient = new FakeClient();
		_clientSource = new ClientSource(_fakeClient);
	}

	public Task<Events.ConfigurationUpdated?> ReadConfigurationEvent(CancellationToken token) => _clientSource.ReadConfigurationEvent(token);

	public IAsyncEnumerable<IEvent> ReadAutoScavengeEvents(CancellationToken token) => _clientSource.ReadAutoScavengeEvents(token);

	public void AddConfigurationEvent(Events.ConfigurationUpdated @event) {
		var serializerOptions = new JsonSerializerOptions() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new CrontableScheduleJsonConverter() }
		};

		var data = JsonSerializer.Serialize(@event, serializerOptions);
		_fakeClient.AddEvent(StreamNames.AutoScavengeConfiguration, @event, Encoding.UTF8.GetBytes(data));
	}

	public void AddScavengeEvent(IEvent @event) {
		var serializerOptions = new JsonSerializerOptions() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new EventJsonConverter() }
		};

		var data = JsonSerializer.Serialize(@event, serializerOptions);
		_fakeClient.AddEvent(StreamNames.AutoScavenges, @event, Encoding.UTF8.GetBytes(data));
	}
}
