// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace ReleaseManager;

public static class Interactively {
	public static async Task MovePackages(
		CloudsmithClient client,
		string query,
		string sourceRepo,
		string destinationRepo) {

		var packages = await client.ListPackages(sourceRepo, query);
		foreach (var package in packages)
			Console.WriteLine(package.Pretty);

		Console.WriteLine("Press Y to move {0} packages from {1} to {2}",
			packages.Length, sourceRepo, destinationRepo);

		if (Console.ReadKey().Key != ConsoleKey.Y)
			return;

		Console.WriteLine();

		foreach (var package in packages) {
			Console.WriteLine("Moving {0}", package.Pretty);
			await client.MovePackage(package, destinationRepo);
		}

		Console.WriteLine("Done");
	}
}
