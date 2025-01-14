// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EventStore.Client;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Auth.OAuth.Tests;

public class EventStoreCloudTokenIntegrationTests {
	private readonly ITestOutputHelper _output;

	public EventStoreCloudTokenIntegrationTests(ITestOutputHelper output) {
		_output = output;
	}

	[Fact]
	public async Task cloud_token() {
		using var fixture = await Fixture.Create(_output);
		var token = await fixture.IdentityServer.GetRefreshToken("admin", "password");

		var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tlsVerifyCert=false")
			.UseEventStoreCloud(IdpFixture.ClientId, fixture.IdentityServer.HttpClient);
		settings.LoggerFactory = new XUnitLoggerFactory(_output);
		using var client = new EventStoreClient(settings);

		await client.AppendToStreamAsync("a-stream", StreamState.Any, new[] {
			new EventData(Uuid.NewUuid(), "-", ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
		}, userCredentials: new UserCredentials(token));

		await client.ReadAllAsync(Direction.Forwards, Position.Start, userCredentials: new UserCredentials(token))
			.ToArrayAsync();
	}
}
