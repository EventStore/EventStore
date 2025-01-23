// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode;

public class MessageForwardingProxy {
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

	private readonly ConcurrentDictionary<Guid, Forwarding> _forwardings =
		new ConcurrentDictionary<Guid, Forwarding>();

	public void Register(Guid internalCorrId, Guid clientCorrId, IEnvelope envelope, TimeSpan timeout,
		Message timeoutMessage) {
		_forwardings.AddOrUpdate(
			internalCorrId,
			new Forwarding(clientCorrId, envelope, _stopwatch.Elapsed + timeout, timeoutMessage),
			(x, y) => {
				throw new Exception(
					string.Format("Forwarding for InternalCorrId {0:B} (ClientCorrId {1:B}) already exists.",
						internalCorrId, clientCorrId));
			});
	}

	public bool TryForwardReply<TMessage>(Guid correlationId, TMessage originalMessage,
		Func<Guid, TMessage, TMessage> getForwardMessage)
		where TMessage : Message {
		if (!_forwardings.TryRemove(correlationId, out var forwarding)) return false;
		forwarding.Envelope.ReplyWith(getForwardMessage(forwarding.ClientCorrId, originalMessage));
		return true;
	}

	public void TimeoutForwardings() {
		var now = _stopwatch.Elapsed;

		foreach (var forwPair in _forwardings) {
			if (forwPair.Value.TimeoutTimestamp <= now && _forwardings.TryRemove(forwPair.Key, out var forwarding))
				forwarding.Envelope.ReplyWith(forwarding.TimeoutMessage);
		}
	}

	private class Forwarding {
		public readonly Guid ClientCorrId;
		public readonly IEnvelope Envelope;
		public readonly TimeSpan TimeoutTimestamp;
		public readonly Message TimeoutMessage;

		public Forwarding(Guid clientCorrId, IEnvelope envelope, TimeSpan timeoutTimestamp,
			Message timeoutMessage) {
			Ensure.NotNull(envelope, "envelope");
			Ensure.NotNull(timeoutMessage, "timeoutMessage");

			ClientCorrId = clientCorrId;
			Envelope = envelope;
			TimeoutTimestamp = timeoutTimestamp;
			TimeoutMessage = timeoutMessage;
		}
	}
}
