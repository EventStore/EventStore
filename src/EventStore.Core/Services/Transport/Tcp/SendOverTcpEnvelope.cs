// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp;

public class SendOverTcpEnvelope : IEnvelope {
	private readonly IPublisher _networkSendQueue;
	private readonly TcpConnectionManager _manager;

	public SendOverTcpEnvelope(TcpConnectionManager manager, IPublisher networkSendQueue) {
		Ensure.NotNull(manager, "manager");
		Ensure.NotNull(networkSendQueue, "networkSendQueue");
		_networkSendQueue = networkSendQueue;
		_manager = manager;
	}

	public void ReplyWith<T>(T message) where T : Message {
		if (_manager != null && !_manager.IsClosed) {
			_networkSendQueue.Publish(new TcpMessage.TcpSend(_manager, message));
		}
	}
}
