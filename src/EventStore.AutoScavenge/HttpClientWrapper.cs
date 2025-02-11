// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common;

namespace EventStore.AutoScavenge;

// An HttpClient with additional context
public class HttpClientWrapper {
	public HttpClient HttpClient { get; }
	public string Protocol { get; }

	public HttpClientWrapper(EventStoreOptions options, INodeHttpClientFactory nodeHttpClientFactory) {
		var nodeHttpClient =
			nodeHttpClientFactory.CreateHttpClient(options.DiscoverViaDns ? [options.ClusterDns] : null);

		Protocol = options.Insecure ? "http" : "https";

		HttpClient = nodeHttpClient;
		HttpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
		HttpClient.Timeout = TimeSpan.FromSeconds(1);
	}
}
