// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace ReleaseManager;

internal class Program {
	static async Task Main(string[] args) {
		using var client = new CloudsmithClient(
			owner: "eventstore",
			Environment.GetEnvironmentVariable("CLOUDSMITH_API_KEY")
				?? throw new Exception("Missing CLOUDSMITH_API_KEY environment variable"));

		await MoveToPreview(client, "25.0.0-rc.2");
	}

	static async Task MoveToPreview(CloudsmithClient client, string query) {
		var sourceRepo = "eventstore-staging-ee";
		var destinationRepo = "eventstore-preview";
		await Interactively.MovePackages(client, query, sourceRepo, destinationRepo);
	}
}
