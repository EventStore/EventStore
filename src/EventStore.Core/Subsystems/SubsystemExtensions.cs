// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.IO.Core;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core.Subsystems;

public static class SubsystemExtensions {
	public static IServiceCollection AddConnectedSubsystem(this IServiceCollection services) {
		services.AddSingleton<IClient, InternalClient>();
		services.AddSingleton<IOperationsClient, InternalOperationsClient>();
		return services;
	}
}
