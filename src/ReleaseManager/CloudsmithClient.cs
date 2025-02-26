// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Text.Json;
using ReleaseManager.Model;
using RestSharp;

namespace ReleaseManager;

public sealed class CloudsmithClient(string owner, string apiKey) : IDisposable {
	private readonly IRestClient _client =
		new RestClient(new RestClientOptions() {
			BaseUrl = new("https://api.cloudsmith.io/v1/"),
			ThrowOnAnyError = true,
		}).AddDefaultHeaders(new() {
			{ "accept", "application/json"},
			{ "X-Api-Key", apiKey },
		});

	public void Dispose() {
		_client.Dispose();
	}

	public async Task<Package[]> ListPackages(string repo, string query) {
		var request = new RestRequest($"packages/{owner}/{repo}/?query={query}&sort=-name");
		var response = await _client.GetAsync(request);
		return JsonSerializer.Deserialize<Package[]>(response.Content!)!;
	}

	public async Task MovePackage(Package package, string destinationRepo) {
		var request = new RestRequest($"packages/{owner}/{package.repository}/{package.identifier_perm}/move/");
		request.AddJsonBody(new {
			destination = destinationRepo,
		});

		await _client.PostAsync(request);
	}
}
