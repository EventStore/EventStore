using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Commit;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication {
	public abstract class RequestManagerSpecification<TManager>
		where TManager : RequestManagerBase {
		protected static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
		protected static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);

		protected TManager Manager;
		protected List<Message> Produced;
		protected FakePublisher Publisher = new FakePublisher();
		protected Guid InternalCorrId = Guid.NewGuid();
		protected Guid ClientCorrId = Guid.NewGuid();
		protected byte[] Metadata = new byte[255];
		protected byte[] EventData = new byte[255];
		protected FakeEnvelope Envelope = new FakeEnvelope();
		protected ICommitSource CommitSource = new CommitSource();
		protected InMemoryBus Dispatcher = new InMemoryBus(nameof(RequestManagerSpecification<TManager>));

		protected abstract TManager OnManager(FakePublisher publisher);
		protected abstract IEnumerable<Message> WithInitialMessages();
		protected abstract Message When();

		protected Event DummyEvent() {
			return new Event(Guid.NewGuid(), "test", false, EventData, Metadata);
		}
		public RequestManagerSpecification() {
			Dispatcher.Subscribe<CommitMessage.CommittedTo>(CommitSource);
			Dispatcher.Subscribe<CommitMessage.ReplicatedTo>(CommitSource);
		}
		[SetUp]
		public virtual void Setup() {
			Envelope.Replies.Clear();
			Publisher.Messages.Clear();

			Manager = OnManager(Publisher);
			Dispatcher.Subscribe<StorageMessage.CheckStreamAccessCompleted>(Manager);
			Dispatcher.Subscribe<StorageMessage.PrepareAck>(Manager);
			Dispatcher.Subscribe<StorageMessage.CommitAck>(Manager);
			Dispatcher.Subscribe<StorageMessage.InvalidTransaction>(Manager);
			Dispatcher.Subscribe<StorageMessage.StreamDeleted>(Manager);
			Dispatcher.Subscribe<StorageMessage.WrongExpectedVersion>(Manager);
			Dispatcher.Subscribe<StorageMessage.AlreadyCommitted>(Manager);
			Dispatcher.Subscribe<StorageMessage.RequestManagerTimerTick>(Manager);
			var txCommitMrg = Manager as TransactionCommit;
			if (txCommitMrg != null) {
				Dispatcher.Subscribe<StorageMessage.CommitIndexed>(txCommitMrg);
			}

			Manager.Start();

			foreach (var msg in WithInitialMessages()) {
				Dispatcher.Publish(msg);
			}

			Publisher.Messages.Clear();
			Dispatcher.Publish(When());
			Produced = new List<Message>(Publisher.Messages);
		}
	}
}
