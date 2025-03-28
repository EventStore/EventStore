// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

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
