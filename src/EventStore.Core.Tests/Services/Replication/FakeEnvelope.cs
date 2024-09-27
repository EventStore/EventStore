// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Services.Replication {
	public class FakeEnvelope : IEnvelope {
		public List<Message> Replies = new List<Message>();

		public void ReplyWith<T>(T message) where T : Message {
			Replies.Add(message);
		}
	}
}
