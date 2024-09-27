// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messaging {
	class PublishToWrapEnvelop : IEnvelope {
		private readonly IPublisher _publisher;
		private readonly IEnvelope _nestedEnevelop;

		public PublishToWrapEnvelop(IPublisher publisher, IEnvelope nestedEnevelop) {
			_publisher = publisher;
			_nestedEnevelop = nestedEnevelop;
		}

		public void ReplyWith<T>(T message) where T : Message {
			_publisher.Publish(new UnwrapEnvelopeMessage(() => _nestedEnevelop.ReplyWith(message)));
		}
	}
}
