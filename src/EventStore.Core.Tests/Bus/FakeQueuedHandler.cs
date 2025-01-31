// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Tests.Bus;

public class FakeQueuedHandler : IQueuedHandler {
	public string Name => string.Empty;
	public void Handle(Message message) { }
	public void Publish(Message message) { }
	public Task Start() => Task.CompletedTask;
	public Task Stop() => Task.CompletedTask;
	public void RequestStop() { }
	public QueueStats GetStatistics() => null;
}
