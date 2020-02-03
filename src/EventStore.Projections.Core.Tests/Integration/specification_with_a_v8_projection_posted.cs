using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.projections_manager;

namespace EventStore.Projections.Core.Tests.Integration {
	public abstract class specification_with_a_v8_projection_posted
		: TestFixtureWithProjectionCoreAndManagementServices {
		protected string _projectionName = "test-projection";

		protected override void Given() {
			base.Given();
			AllWritesSucceed();
			NoOtherStreams();
			GivenEvents();
			EnableReadAll();
		}

		protected override Tuple<IBus, IPublisher, InMemoryBus, TimeoutScheduler, Guid>[] GivenProcessingQueues() {
			var buses = new IBus[] {new InMemoryBus("1"), new InMemoryBus("2")};
			var outBuses = new[] {new InMemoryBus("o1"), new InMemoryBus("o2")};
			_otherQueues = new ManualQueue[]
				{new ManualQueue(buses[0], _timeProvider), new ManualQueue(buses[1], _timeProvider)};
			return new[] {
				Tuple.Create(
					buses[0],
					(IPublisher)_otherQueues[0],
					outBuses[0],
					default(TimeoutScheduler),
					Guid.NewGuid()),
				Tuple.Create(
					buses[1],
					(IPublisher)_otherQueues[1],
					outBuses[1],
					default(TimeoutScheduler),
					Guid.NewGuid())
			};
		}

		protected virtual void GivenEvents() {}

		protected abstract ProjectionManagementMessage.Command.Post GivenProjection();

		protected ProjectionManagementMessage.Command.Post CreateNewProjectionMessage
			(string name, string source, bool subscribeFromEnd = false) {
			return new ProjectionManagementMessage.Command.Post(
				new PublishEnvelope(_bus), ProjectionMode.Continuous, name, ProjectionManagementMessage.RunAs.System,
				"JS", source, enabled: true, checkpointsEnabled: true, trackEmittedStreams: true, emitEnabled: true,
				subscribeFromEnd: subscribeFromEnd);
		}

		protected override IEnumerable<WhenStep> When() {
			yield return (new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid()));

			yield return GivenProjection();
		}
	}
}
