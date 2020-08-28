﻿using EventStore.Core.TransactionLog.Hashes;
using EventStore.Core.TransactionLog.Services;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy
{
	using Common.Utils;
	using Data;
	using PinnedState;

	internal abstract class PinnablePersistentSubscriptionConsumerStrategy : IPersistentSubscriptionConsumerStrategy {
		private IHasher _hash;
		protected static readonly char LinkToSeparator = '@';
		private readonly PinnedConsumerState _state = new PinnedConsumerState();
		private readonly object _stateLock = new object();

		public PinnablePersistentSubscriptionConsumerStrategy(IHasher streamHasher) {
			_hash = streamHasher;
		}

		public abstract string Name { get; }

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
			string sourceStreamId = GetAssignmentSourceId(ev);

			return _hash.Hash(sourceStreamId) % (uint)_state.Assignments.Length;
		}

		protected abstract string GetAssignmentSourceId(ResolvedEvent ev);

		protected string GetSourceStreamId(ResolvedEvent ev)
		{
			var eventRecord = ev.Event ?? ev.Link; // Unresolved link just use the link

			string sourceStreamId = eventRecord.EventStreamId;

			if (eventRecord.EventType == SystemEventTypes.LinkTo) // Unresolved link.
			{
				sourceStreamId = Helper.UTF8NoBom.GetString(eventRecord.Data.Span);
				int separatorIndex = sourceStreamId.IndexOf(LinkToSeparator);
				if (separatorIndex != -1)
				{
					sourceStreamId = sourceStreamId.Substring(separatorIndex + 1);
				}
			}

			return sourceStreamId;
		}
	}
}
