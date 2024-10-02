// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public struct RetryableMessage {
		public readonly Guid MessageId;
		public readonly DateTime DueTime;

		public RetryableMessage(Guid messageId, DateTime dueTime) {
			MessageId = messageId;
			DueTime = dueTime;
		}
	}
}
