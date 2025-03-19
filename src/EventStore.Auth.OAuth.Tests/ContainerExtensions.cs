// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Xunit.Abstractions;

namespace EventStore.Auth.OAuth.Tests;

internal static class ContainerExtensions {
	public static void ShipContainerLogs(this IContainerService container, ITestOutputHelper testOutputHelper) =>
		Task.Run(() => {
			using var logs = container.Logs(true);
			foreach (var line in logs.ReadToEnd()) {
				testOutputHelper.WriteLine(line);
			}
		});
}
