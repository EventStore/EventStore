// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using EventStore.POC.ConnectorsEngine.Processing.Sinks;
using EventStore.POC.IO.Core;

namespace Eventstore.POC.Tests.Processing;

public class UrlParserTests {
	private const string BaseAddress = "https://example.org";

	[Theory]
	[InlineData("https://example.org/api/{{eventType}}/{{stream}}/{{category}}", "https://example.org")]
	[InlineData("http://example.org:80/api/{{eventType}}/{{stream}}/{{category}}", "http://example.org:80")]
	[InlineData("https://localhost:8080/receivers/connector-one", "https://localhost:8080")]
	[InlineData("http://localhost:8080/receivers/connector-one", "http://localhost:8080")]
	public void should_get_absolute_uri(string inputUri, string expectedBaseAddress) {
		var uri = HttpSink.UrlParser.GetBaseAddress(new Uri(inputUri));
		Assert.Equal(new Uri(expectedBaseAddress), uri);
	}

	[Theory]
	[InlineData("warpCoreEjected", "criticalEvents", $"{BaseAddress}/api/{{eventType}}/{{stream}}/{{category}}", "/api/warpCoreEjected/criticalEvents/criticalEvents")]
	[InlineData("accountCreated", "account-124", $"{BaseAddress}/api/{{eventType}}/{{stream}}/{{category}}", "/api/accountCreated/account-124/account")]
	[InlineData("accountCreated", "account-124", $"{BaseAddress}/api/{{eventType}}", "/api/accountCreated")]
	[InlineData("accountCreated", "account-124", $"{BaseAddress}/api/{{other}}", "/api/{other}")]
	[InlineData("你好", "account-124", $"{BaseAddress}/api/{{eventType}}", "/api/你好")]
	[InlineData("", "account-124", $"{BaseAddress}/api/{{eventType}}", "/api/")]
	public void should_replace_segments(string eventType, string stream, string pattern, string expectedUrl) {
		var evt = new Event(
			eventId: Guid.NewGuid(),
			created: DateTime.Now,
			stream: stream,
			eventNumber: 0,
			eventType: eventType,
			contentType: "application/json",
			commitPosition: 1,
			preparePosition: 1,
			isRedacted: false,
			data: new ReadOnlyMemory<byte>(),
			metadata: new ReadOnlyMemory<byte>()
		);

		var path = HttpSink.UrlParser.GetPath(new Uri(pattern), evt);
		Assert.Equal(expectedUrl, path);
	}
}
