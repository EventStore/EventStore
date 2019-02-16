using System;
using System.Linq;
using NUnit.Framework;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Management;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Replication;
using System.Collections.Generic;

namespace EventStore.Projections.Core.Tests.Services.core_coordinator {
	[TestFixture]
	public class when_starting_with_projection_type_all {
		private FakePublisher[] queues;
		private FakePublisher publisher;
		private ProjectionCoreCoordinator _coordinator;
		private TimeoutScheduler[] timeoutScheduler = { };
		private FakeEnvelope envelope = new FakeEnvelope();

		[SetUp]
		public void Setup() {
			queues = new List<FakePublisher>() {new FakePublisher()}.ToArray();
			publisher = new FakePublisher();

			_coordinator =
				new ProjectionCoreCoordinator(ProjectionType.All, timeoutScheduler, queues, publisher, envelope);
			_coordinator.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
			_coordinator.Handle(new SystemMessage.SystemCoreReady());
			_coordinator.Handle(new SystemMessage.EpochWritten(new EpochRecord(0, 0, Guid.NewGuid(), 0, DateTime.Now)));
		}

		[Test]
		public void should_publish_start_reader_messages() {
			Assert.AreEqual(1, queues[0].Messages.FindAll(x => x is ReaderCoreServiceMessage.StartReader).Count);
		}

		[Test]
		public void should_publish_start_core_messages() {
			Assert.AreEqual(1,
				queues[0].Messages.FindAll(x => x.GetType() == typeof(ProjectionCoreServiceMessage.StartCore)).Count);
		}
	}
}
