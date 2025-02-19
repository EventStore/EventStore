// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.Auth.OAuth.Tests;

public class OAuthAuthenticationHttpIntegrationTests {
	private static readonly string[] EnableAtomPub = {
		"KURRENTDB_ENABLE_ATOM_PUB_OVER_HTTP=true"
	};

	private readonly ITestOutputHelper _output;

	public OAuthAuthenticationHttpIntegrationTests(ITestOutputHelper output) {
		_output = output;
	}

	private StringContent GenerateEvents() {
		var events = new[] {new {EventId = Guid.NewGuid(), EventType = "event-type", Data = new {A = "1"}}};
		return new StringContent(JsonConvert.SerializeObject(events)) {
			Headers = {ContentType = new MediaTypeHeaderValue("application/vnd.eventstore.events+json")}
		};
	}

	[SkipOnWindows]
	public async Task admin() {
		using var fixture = await Fixture.Create(_output,EnableAtomPub);
		var token = await fixture.IdentityServer.GetAccessToken("admin", "password");

		using var client = CreateHttpClient(token);

		using var writeResponse = await client.PostAsync("/streams/a-stream", GenerateEvents());
		Assert.Equal(HttpStatusCode.Created,writeResponse.StatusCode);

		using var writeSystemResponse = await client.PostAsync("/streams/$systemstream", GenerateEvents());
		Assert.Equal(HttpStatusCode.Created,writeSystemResponse.StatusCode);

		using var readResponse = await client.GetAsync("/streams/$all");
		Assert.Equal(HttpStatusCode.OK,readResponse.StatusCode);
	}

	[SkipOnWindows]
	public async Task user() {
		using var fixture = await Fixture.Create(_output, EnableAtomPub);
		var token = await fixture.IdentityServer.GetAccessToken("user", "password");

		using var client = CreateHttpClient(token);

		using var writeResponse = await client.PostAsync("/streams/a-stream", GenerateEvents());
		Assert.Equal(HttpStatusCode.Created,writeResponse.StatusCode);

		using var writeSystemResponse = await client.PostAsync("/streams/$systemstream", GenerateEvents());
		Assert.Equal(HttpStatusCode.Unauthorized,writeSystemResponse.StatusCode);

		using var readResponse = await client.GetAsync("/streams/$all");
		Assert.Equal(HttpStatusCode.Unauthorized,readResponse.StatusCode);
	}


	private static HttpClient CreateHttpClient(string token) {
		var socketsHttpHandler = new SocketsHttpHandler {
			SslOptions = {
				RemoteCertificateValidationCallback = delegate { return true; }
			}
		};

		return new HttpClient(socketsHttpHandler)
		{
			BaseAddress = new UriBuilder {
				Port = 2113,
				Scheme = Uri.UriSchemeHttps
			}.Uri,
			DefaultRequestHeaders = {
				Authorization = new AuthenticationHeaderValue("Bearer", token)
			}
		};
	}
}
