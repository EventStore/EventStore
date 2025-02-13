// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.TcpPlugin.Tests;

public class TcpMessageCollector : IHandle<ClientMessage.ReadEvent> {
	private TaskCompletionSource<ClientMessage.ReadEvent> _source = new();

	public Task<ClientMessage.ReadEvent> Message => _source.Task;
	public void Handle(ClientMessage.ReadEvent message) {
		_source.TrySetResult(message);
	}
}
