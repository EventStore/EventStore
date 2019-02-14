using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription {
	public class StreamBuffer {
		private readonly int _maxBufferSize;
		private readonly int _initialSequence;
		private readonly LinkedList<OutstandingMessage> _retry = new LinkedList<OutstandingMessage>();
		private readonly LinkedList<OutstandingMessage> _buffer = new LinkedList<OutstandingMessage>();

		private readonly BoundedQueue<OutstandingMessage> _liveBuffer;

		public long LiveBufferCount {
			get { return _liveBuffer.Count; }
		}

		public int BufferCount {
			get { return _retry.Count + _buffer.Count; }
		}

		public int RetryBufferCount {
			get { return _retry.Count; }
		}

		public int ReadBufferCount {
			get { return _buffer.Count; }
		}

		public bool Live { get; private set; }

		public bool CanAccept(int count) {
			return _maxBufferSize - BufferCount > count;
		}

		public StreamBuffer(int maxBufferSize, int maxLiveBufferSize, int initialSequence, bool startInHistory) {
			Live = !startInHistory;
			_initialSequence = initialSequence;
			_maxBufferSize = maxBufferSize;
			_liveBuffer = new BoundedQueue<OutstandingMessage>(maxLiveBufferSize);
		}

		private void SwitchToLive() {
			while (_liveBuffer.Count > 0) {
				_buffer.AddLast(_liveBuffer.Dequeue());
			}

			Live = true;
		}

		private void DrainLiveTo(long eventNumber) {
			while (_liveBuffer.Count > 0 && _liveBuffer.Peek().ResolvedEvent.OriginalEventNumber < eventNumber) {
				_liveBuffer.Dequeue();
			}
		}

		public void AddRetry(OutstandingMessage ev) {
			// Insert the retried event before any events with higher version number.

			var retryEventNumber = (ev.ResolvedEvent.Event ?? ev.ResolvedEvent.Link).EventNumber;

			var currentNode = _retry.First;

			while (currentNode != null) {
				var resolvedEvent = currentNode.Value.ResolvedEvent.Event ?? currentNode.Value.ResolvedEvent.Link;
				if (retryEventNumber < resolvedEvent.EventNumber) {
					_retry.AddBefore(currentNode, ev);
					return;
				}

				currentNode = currentNode.Next;
			}

			_retry.AddLast(ev);
		}

		public void AddLiveMessage(OutstandingMessage ev) {
			if (Live) {
				if (_buffer.Count < _maxBufferSize)
					_buffer.AddLast(ev);
				else
					Live = false;
			}

			_liveBuffer.Enqueue(ev);
		}

		public void AddReadMessage(OutstandingMessage ev) {
			if (Live) return;
			if (ev.ResolvedEvent.OriginalEventNumber <= _initialSequence)
				return;
			if (ev.ResolvedEvent.OriginalEventNumber < TryPeekLive()) {
				_buffer.AddLast(ev);
			} else if (ev.ResolvedEvent.OriginalEventNumber > TryPeekLive()) {
				DrainLiveTo(ev.ResolvedEvent.OriginalEventNumber);
				SwitchToLive();
			} else {
				SwitchToLive();
			}
		}

		private long TryPeekLive() {
			return _liveBuffer.Count == 0 ? long.MaxValue : _liveBuffer.Peek().ResolvedEvent.OriginalEventNumber;
		}

		public IEnumerable<OutstandingMessagePointer> Scan() {
			// This enumerator assumes that nothing is added to the buffers during enumeration.

			foreach (var list in new[] {_retry, _buffer}) // save on code duplication
			{
				var current = list.First;
				if (current != null) {
					do {
						// We have to copy next before yielding as the expectation is
						// that current is removed from the list setting next to null.
						var next = current.Next;

						yield return new OutstandingMessagePointer(current);

						current = next;
					} while (current != null);
				}
			}
		}

		public bool TryMoveToLive() {
			if (_liveBuffer.Count == 0) {
				Live = true;
				return true;
			}

			return false;
		}

		public long GetLowestRetry() {
			if (_retry.Count == 0) return long.MaxValue;
			return _retry.Min(x => x.ResolvedEvent.OriginalEventNumber);
		}

		public struct OutstandingMessagePointer {
			private readonly LinkedListNode<OutstandingMessage> _entry;

			internal OutstandingMessagePointer(LinkedListNode<OutstandingMessage> entry)
				: this() {
				_entry = entry;
			}

			public OutstandingMessage Message {
				get { return _entry.Value; }
			}

			public void MarkSent() {
				if (_entry.List == null) {
					throw new InvalidOperationException("The message can only be accepted once.");
				}

				_entry.List.Remove(_entry);
			}
		}
	}

	public enum BufferedStreamReaderState {
		Unknown,
		CatchingUp,
		Live
	}
}
