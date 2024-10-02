// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services {
	public class TcpSendService : IHandle<TcpMessage.TcpSend> {
		public void Handle(TcpMessage.TcpSend message) {
			// todo: histogram metric?
			message.ConnectionManager.SendMessage(message.Message);
		}
	}
}
