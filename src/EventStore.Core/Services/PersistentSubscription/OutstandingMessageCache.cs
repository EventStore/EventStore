using System;
using System.Linq;
using System.Collections.Generic;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription {
	public enum StartMessageResult {
		Success,
		SkippedDuplicate
	}

	public class OutstandingMessageCache {
		private readonly Dictionary<Guid, Tuple<DateTime, OutstandingMessage>> _outstandingRequests;
		private readonly SortedDictionary<Tuple<DateTime, RetryableMessage>, bool> _byTime;
		private readonly SortedList<long, long> _bySequences;

		public class ByTypeComparer : IComparer<Tuple<DateTime, RetryableMessage>> {
			public int Compare(Tuple<DateTime, RetryableMessage> x, Tuple<DateTime, RetryableMessage> y) {
				if (x.Item1 != y.Item1) {
					return x.Item1 < y.Item1 ? -1 : 1;
				}

				var q = y.Item2.MessageId.CompareTo(x.Item2.MessageId);
				return q;
			}
		}

		public OutstandingMessageCache() {
			_outstandingRequests = new Dictionary<Guid, Tuple<DateTime, OutstandingMessage>>();
			_byTime = new SortedDictionary<Tuple<DateTime, RetryableMessage>, bool>(new ByTypeComparer());
			_bySequences = new SortedList<long, long>();
		}

		public int Count {
			get { return _outstandingRequests.Count; }
		}

		public void Remove(Guid messageId) {
			Tuple<DateTime, OutstandingMessage> m;
			if (_outstandingRequests.TryGetValue(messageId, out m)) {
				_byTime.Remove(new Tuple<DateTime, RetryableMessage>(m.Item1,
					new RetryableMessage(m.Item2.EventId, m.Item1)));
				_outstandingRequests.Remove(messageId);
				_bySequences.Remove(m.Item2.ResolvedEvent.OriginalEventNumber);
			}
		}

		public void Remove(IEnumerable<Guid> messageIds) {
			foreach (var m in messageIds) Remove(m);
		}

		public StartMessageResult StartMessage(OutstandingMessage message, DateTime expires) {
			if (_outstandingRequests.ContainsKey(message.EventId))
				return StartMessageResult.SkippedDuplicate;
			_outstandingRequests[message.EventId] = new Tuple<DateTime, OutstandingMessage>(expires, message);
			_bySequences.Add(message.ResolvedEvent.OriginalEventNumber, message.ResolvedEvent.OriginalEventNumber);
			_byTime.Add(new Tuple<DateTime, RetryableMessage>(expires, new RetryableMessage(message.EventId, expires)),
				false);

			return StartMessageResult.Success;
		}

		public IEnumerable<OutstandingMessage> GetMessagesExpiringBefore(DateTime time) {
			while (_byTime.Count > 0) {
				var item = _byTime.Keys.First();
				if (item.Item1 > time) {
					yield break;
				}

				_byTime.Remove(item);
				Tuple<DateTime, OutstandingMessage> m;
				if (_outstandingRequests.TryGetValue(item.Item2.MessageId, out m)) {
					yield return _outstandingRequests[item.Item2.MessageId].Item2;
					_outstandingRequests.Remove(item.Item2.MessageId);
					_bySequences.Remove(m.Item2.ResolvedEvent.OriginalEventNumber);
				}
			}
		}

		public IEnumerable<Tuple<DateTime, RetryableMessage>> WaitingTimeMessages() {
			return _byTime.Keys;
		}

		public long GetLowestPosition() {
			//TODO is there a better way of doing this?
			if (_bySequences.Count == 0) return long.MaxValue;
			return _bySequences.Values[0];
		}

		public bool GetMessageById(Guid id, out OutstandingMessage outstandingMessage) {
			outstandingMessage = new OutstandingMessage();
			Tuple<DateTime, OutstandingMessage> m;
			if (_outstandingRequests.TryGetValue(id, out m)) {
				outstandingMessage = m.Item2;
				return true;
			}

			return false;
		}
	}
}
