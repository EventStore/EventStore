// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.Core.Authentication;

public record AuthenticationProviderFactoryComponents {
	public IPublisher MainQueue { get; init; }
	public ISubscriber MainBus { get; init; }
	public IPublisher WorkersQueue { get; init; }
	public InMemoryBus[] WorkerBuses { get; init; }
	public HttpSendService HttpSendService { get; init; }
	public IUriRouter Router { get; init; }
}
