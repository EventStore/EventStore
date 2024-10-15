// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using EventStore.Licensing.Keygen;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Text.Json;
using System.Text;
using System.Net;

namespace EventStore.Licensing.Tests.Keygen;

partial class KeygenSimulator : HttpMessageHandler {
	readonly Channel<HttpRequestMessage> _requests;
	readonly Channel<HttpResponseMessage> _responses;
	readonly JsonSerializerOptions _serializerOptions;
	readonly string _fingerprint = new Fingerprint().Get();

	public KeygenSimulator() {
		_requests = Channel.CreateUnbounded<HttpRequestMessage>();
		_responses = Channel.CreateUnbounded<HttpResponseMessage>();
		_serializerOptions = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {

		await _requests.Writer.WriteAsync(request, cancellationToken);
		return await _responses.Reader.ReadAsync(cancellationToken);
	}


	ValueTask<HttpRequestMessage> Receive() => _requests.Reader.ReadAsync();

	async Task Send<TResponse>(HttpStatusCode httpStatusCode, TResponse response) {
		var content = JsonSerializer.Serialize(response, _serializerOptions);
		await _responses.Writer.WriteAsync(new HttpResponseMessage {
			StatusCode = httpStatusCode,
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		});
	}
}
