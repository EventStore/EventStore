// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription;

public enum StartMessageResult {
	Success,
	SkippedDuplicate
}

public class OutstandingMessageCache {
	private readonly Dictionary<Guid, Tuple<DateTime, OutstandingMessage>> _outstandingRequests = new();
	private readonly SortedDictionary<Tuple<DateTime, RetryableMessage>, bool> _byTime = new(new ByTypeComparer());
	private readonly SortedList<long, OutstandingMessage> _bySequences = new();

	public class ByTypeComparer : IComparer<Tuple<DateTime, RetryableMessage>> {
		public int Compare(Tuple<DateTime, RetryableMessage> x, Tuple<DateTime, RetryableMessage> y) {
			if (x.Item1 != y.Item1) {
				return x.Item1 < y.Item1 ? -1 : 1;
			}

			var q = y.Item2.MessageId.CompareTo(x.Item2.MessageId);
			return q;
		}
	}

	public int Count => _outstandingRequests.Count;

	public void Remove(Guid messageId) {
		if (!_outstandingRequests.TryGetValue(messageId, out var m)) return;
		_byTime.Remove(new(m.Item1, new(m.Item2.EventId, m.Item1)));
		_outstandingRequests.Remove(messageId);
		if (m.Item2.EventSequenceNumber.HasValue) {
			_bySequences.Remove(m.Item2.EventSequenceNumber.Value);
		}
	}

	public void Remove(IEnumerable<Guid> messageIds) {
		foreach (var m in messageIds) Remove(m);
	}

	public StartMessageResult StartMessage(OutstandingMessage message, DateTime expires) {
		if (_outstandingRequests.ContainsKey(message.EventId))
			return StartMessageResult.SkippedDuplicate;
		_outstandingRequests[message.EventId] = new(expires, message);
		Debug.Assert(message.IsReplayedEvent || message.EventSequenceNumber.HasValue);
		if (message.EventSequenceNumber.HasValue) {
			_bySequences.Add(message.EventSequenceNumber.Value, message);
		}
		_byTime.Add(new(expires, new(message.EventId, expires)), false);

		return StartMessageResult.Success;
	}

	public IEnumerable<OutstandingMessage> GetMessagesExpiringBefore(DateTime time) {
		while (_byTime.Count > 0) {
			var item = _byTime.Keys.First();
			if (item.Item1 > time) {
				yield break;
			}

			_byTime.Remove(item);
			if (_outstandingRequests.TryGetValue(item.Item2.MessageId, out var m)) {
				yield return _outstandingRequests[item.Item2.MessageId].Item2;
				_outstandingRequests.Remove(item.Item2.MessageId);
				if (m.Item2.EventSequenceNumber.HasValue) {
					_bySequences.Remove(m.Item2.EventSequenceNumber.Value);
				}
			}
		}
	}

	public (OutstandingMessage? message, long sequenceNumber) GetLowestPosition() {
		foreach(var x in _bySequences) {
			if (!x.Value.IsReplayedEvent)
				return (x.Value, x.Key);
		}
		return (null, long.MaxValue);
	}

	public bool GetMessageById(Guid id, out OutstandingMessage outstandingMessage) {
		outstandingMessage = new();
		if (!_outstandingRequests.TryGetValue(id, out var m)) return false;
		outstandingMessage = m.Item2;
		return true;
	}

	// Only used in tests
	public IEnumerable<Tuple<DateTime, RetryableMessage>> WaitingTimeMessages() {
		return _byTime.Keys;
	}
}
