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
	public class when_restarting_with_projection_type_none {
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
				new ProjectionCoreCoordinator(ProjectionType.None, timeoutScheduler, queues, publisher, envelope);
			_coordinator.Handle(new SystemMessage.SystemCoreReady());
			_coordinator.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
			_coordinator.Handle(new SystemMessage.EpochWritten(new EpochRecord(0, 0, Guid.NewGuid(), 0, DateTime.Now)));

			//force stop
			_coordinator.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));

			//clear queues for clearer testing
			queues[0].Messages.Clear();
		}

		private void BecomeReady() {
			//become ready
			_coordinator.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
			_coordinator.Handle(new SystemMessage.EpochWritten(new EpochRecord(0, 0, Guid.NewGuid(), 0, DateTime.Now)));
		}

		private void AllSubComponentsStarted() {
			_coordinator.Handle(new ProjectionCoreServiceMessage.SubComponentStarted("EventReaderCoreService"));
		}

		private void AllSubComponentsStopped() {
			_coordinator.Handle(new ProjectionCoreServiceMessage.SubComponentStopped("EventReaderCoreService"));
		}

		[Test]
		public void should_not_start_if_subcomponents_not_stopped() {
			AllSubComponentsStarted();

			BecomeReady();
			Assert.AreEqual(0, queues[0].Messages.FindAll(x => x is ReaderCoreServiceMessage.StartReader).Count);
		}

		[Test]
		public void should_start_if_subcomponents_stopped_before_becoming_ready() {
			AllSubComponentsStarted();

			AllSubComponentsStopped();
			BecomeReady();
			Assert.AreEqual(1, queues[0].Messages.FindAll(x => x is ReaderCoreServiceMessage.StartReader).Count);
		}

		[Test]
		public void should_start_if_subcomponents_stopped_after_becoming_ready() {
			AllSubComponentsStarted();

			BecomeReady();
			AllSubComponentsStopped();
			Assert.AreEqual(1, queues[0].Messages.FindAll(x => x is ReaderCoreServiceMessage.StartReader).Count);
		}

		[Test]
		public void should_start_if_subcomponents_started_and_stopped_late_after_becoming_ready() {
			BecomeReady();
			AllSubComponentsStarted();
			AllSubComponentsStopped();
			Assert.AreEqual(1, queues[0].Messages.FindAll(x => x is ReaderCoreServiceMessage.StartReader).Count);
		}
	}
}
