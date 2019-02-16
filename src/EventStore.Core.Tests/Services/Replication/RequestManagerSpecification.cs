using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication {
	public abstract class RequestManagerSpecification {
		protected static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
		protected static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(5);

		protected TwoPhaseRequestManagerBase Manager;
		protected List<Message> Produced;
		protected FakePublisher Publisher;
		protected Guid InternalCorrId = Guid.NewGuid();
		protected Guid ClientCorrId = Guid.NewGuid();
		protected byte[] Metadata = new byte[255];
		protected byte[] EventData = new byte[255];
		protected FakeEnvelope Envelope;

		protected abstract TwoPhaseRequestManagerBase OnManager(FakePublisher publisher);
		protected abstract IEnumerable<Message> WithInitialMessages();
		protected abstract Message When();

		protected Event DummyEvent() {
			return new Event(Guid.NewGuid(), "test", false, EventData, Metadata);
		}

		[SetUp]
		public void Setup() {
			Publisher = new FakePublisher();
			Envelope = new FakeEnvelope();
			Manager = OnManager(Publisher);
			foreach (var m in WithInitialMessages()) {
				Manager.AsDynamic().Handle(m);
			}

			Publisher.Messages.Clear();
			Manager.AsDynamic().Handle(When());
			Produced = new List<Message>(Publisher.Messages);
		}
	}
}
