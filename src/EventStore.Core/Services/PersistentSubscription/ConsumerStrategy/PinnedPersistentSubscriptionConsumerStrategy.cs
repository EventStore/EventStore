using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy.PinnedState;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	class PinnedPersistentSubscriptionConsumerStrategy : IPersistentSubscriptionConsumerStrategy {
		private readonly IHasher _hash;
		private static readonly char LinkToSeparator = '@';
		private readonly PinnedConsumerState _state = new PinnedConsumerState();
		private readonly object _stateLock = new object();

		public PinnedPersistentSubscriptionConsumerStrategy(IHasher streamHasher) {
			_hash = streamHasher;
		}

		public string Name {
			get { return SystemConsumerStrategies.Pinned; }
		}

		public int AvailableCapacity {
			get {
				if (_state == null)
					return 0;

				return _state.AvailableCapacity;
			}
		}

		public void ClientAdded(PersistentSubscriptionClient client) {
			lock (_stateLock) {
				var newNode = new Node(client);

				_state.AddNode(newNode);

				client.EventConfirmed += OnEventRemoved;
			}
		}

		public void ClientRemoved(PersistentSubscriptionClient client) {
			var nodeId = client.CorrelationId;

			client.EventConfirmed -= OnEventRemoved;

			_state.DisconnectNode(nodeId);
		}

		public ConsumerPushResult PushMessageToClient(ResolvedEvent ev, int retryCount) {
			if (_state == null) {
				return ConsumerPushResult.NoMoreCapacity;
			}

			if (_state.AvailableCapacity == 0) {
				return ConsumerPushResult.NoMoreCapacity;
			}


			uint bucket = GetAssignmentId(ev);

			if (_state.Assignments[bucket].State != BucketAssignment.BucketState.Assigned) {
				_state.AssignBucket(bucket);
			}

			if (!_state.Assignments[bucket].Node.Client.Push(ev, retryCount)) {
				return ConsumerPushResult.Skipped;
			}

			_state.RecordEventSent(bucket);
			return ConsumerPushResult.Sent;
		}

		private void OnEventRemoved(PersistentSubscriptionClient client, ResolvedEvent ev) {
			var assignmentId = GetAssignmentId(ev);
			_state.EventRemoved(client.CorrelationId, assignmentId);
		}

		private uint GetAssignmentId(ResolvedEvent ev) {
			string sourceStreamId = GetSourceStreamId(ev);

			return _hash.Hash(sourceStreamId) % (uint)_state.Assignments.Length;
		}

		private static string GetSourceStreamId(ResolvedEvent ev) {
			var eventRecord = ev.Event ?? ev.Link; // Unresolved link just use the link

			string sourceStreamId = eventRecord.EventStreamId;

			if (eventRecord.EventType == SystemEventTypes.LinkTo) // Unresolved link.
			{
				sourceStreamId = Helper.UTF8NoBom.GetString(eventRecord.Data);
				int separatorIndex = sourceStreamId.IndexOf(LinkToSeparator);
				if (separatorIndex != -1) {
					sourceStreamId = sourceStreamId.Substring(separatorIndex + 1);
				}
			}

			return sourceStreamId;
		}
	}
}
