// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.AwakeReaderService;

public partial class AwakeServiceMessage {
	[DerivedMessage(CoreMessage.Awake)]
	public sealed partial class SubscribeAwake : Message {
		public readonly IEnvelope Envelope;
		public readonly Guid CorrelationId;
		public readonly string StreamId;
		public readonly TFPos From;
		public readonly Message ReplyWithMessage;

		public SubscribeAwake(
			IEnvelope envelope, Guid correlationId, string streamId, TFPos @from, Message replyWithMessage) {
			StreamId = streamId;
			From = @from;
			ReplyWithMessage = replyWithMessage;
			Envelope = envelope;
			CorrelationId = correlationId;
		}
	}

	[DerivedMessage(CoreMessage.Awake)]
	public sealed partial class UnsubscribeAwake : Message {
		public readonly Guid CorrelationId;

		public UnsubscribeAwake(Guid correlationId) {
			CorrelationId = correlationId;
		}
	}
}
