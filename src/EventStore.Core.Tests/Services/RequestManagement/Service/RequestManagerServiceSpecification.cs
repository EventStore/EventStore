using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RequestManagement.Service {
	public abstract class RequestManagerServiceSpecification:
		IHandle<StorageMessage.WritePrepares>,
		IHandle<StorageMessage.RequestCompleted> {
		protected readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
		protected readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);

		protected List<Message> Produced = new List<Message>();
		protected FakePublisher Publisher = new FakePublisher();
		protected Guid InternalCorrId = Guid.NewGuid();
		protected Guid ClientCorrId = Guid.NewGuid();
		protected byte[] Metadata = Helper.UTF8NoBom.GetBytes("{Value:42}");
		protected byte[] EventData = Helper.UTF8NoBom.GetBytes("{Value:43}");
		protected FakeEnvelope Envelope = new FakeEnvelope();
		protected InMemoryBus Dispatcher = new InMemoryBus(nameof(RequestManagerServiceSpecification));
		protected RequestManagementService Service;
		protected bool GrantAccess =true;
		protected long LogPosition = 100;
		protected PrepareFlags PrepareFlags = PrepareFlags.Data;
		protected string StreamId = $"{nameof(RequestManagerServiceSpecification)}-{Guid.NewGuid()}";

		protected abstract void Given();
		protected abstract Message When();


		protected RequestManagerServiceSpecification() {
			Dispatcher.Subscribe<StorageMessage.WritePrepares>(this);
			Dispatcher.Subscribe<StorageMessage.RequestCompleted>(this);

			Service = new RequestManagementService(
				Dispatcher,
				TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(2));
			Dispatcher.Subscribe<ClientMessage.WriteEvents>(Service);
			Dispatcher.Subscribe<StorageMessage.PrepareAck>(Service);
			Dispatcher.Subscribe<StorageMessage.InvalidTransaction>(Service);
			Dispatcher.Subscribe<StorageMessage.StreamDeleted>(Service);
			Dispatcher.Subscribe<StorageMessage.WrongExpectedVersion>(Service);
			Dispatcher.Subscribe<StorageMessage.AlreadyCommitted>(Service);
			Dispatcher.Subscribe<StorageMessage.RequestManagerTimerTick>(Service);
			Dispatcher.Subscribe<StorageMessage.CommitIndexed>(Service);
			Dispatcher.Subscribe<ReplicationTrackingMessage.IndexedTo>(Service);
			Dispatcher.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(Service);
		}
		[OneTimeSetUp]
		public virtual void Setup() {
			Envelope.Replies.Clear();
			Publisher.Messages.Clear();

			Given();
			Envelope.Replies.Clear();
			Publisher.Messages.Clear();

			Dispatcher.Publish(When());
			
		}


		private static readonly StreamAcl PublicStream = new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All,
			SystemRoles.All, SystemRoles.All);
		public void Handle(StorageMessage.EffectiveStreamAclRequest message) {
			message.Envelope.ReplyWith(new StorageMessage.EffectiveStreamAclResponse(new StorageMessage.EffectiveAcl(
				PublicStream, PublicStream, PublicStream
				)));
		}

		public void Handle(StorageMessage.WritePrepares message) {

			var transactionPosition = LogPosition;
			foreach (var _ in message.Events) {
				Dispatcher.Publish(new StorageMessage.PrepareAck(
										message.CorrelationId,
										LogPosition,
										PrepareFlags));
				LogPosition += 100;
			}
			Dispatcher.Publish(new StorageMessage.CommitIndexed(
									message.CorrelationId, 
									LogPosition, 
									transactionPosition,
									0, 
									message.Events.Length));
		}

		protected Event DummyEvent() {
			return new Event(Guid.NewGuid(), "SomethingHappened", true, EventData, Metadata);
		}

		public void Handle(StorageMessage.RequestCompleted message) {
			Produced.Add(message);
		}
	}
}
