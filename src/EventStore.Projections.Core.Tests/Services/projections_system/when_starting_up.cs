using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;
using EventStore.Core.Messages;
using System;
using System.Net;

namespace EventStore.Projections.Core.Tests.Services.projections_system {
	namespace startup {
		[TestFixture]
		public class when_starting_with_empty_db : with_projections_subsystem {
			protected override IEnumerable<WhenStep> When() {
				yield return
					new ProjectionManagementMessage.Command.GetStatistics(Envelope, ProjectionMode.AllNonTransient,
						null, false)
					;
			}

			[Test]
			public void system_projections_are_registered() {
				var statistics = HandledMessages.OfType<ProjectionManagementMessage.Statistics>().LastOrDefault();
				Assert.NotNull(statistics);
				Assert.AreEqual(5, statistics.Projections.Length);
			}

			[Test]
			public void system_projections_are_running() {
				var statistics = HandledMessages.OfType<ProjectionManagementMessage.Statistics>().LastOrDefault();
				Assert.NotNull(statistics);
				Assert.That(statistics.Projections.All(s => s.Status == "Stopped"));
			}

			[Test]
			public void core_readers_should_use_the_unique_id_provided_by_the_component_start_message() {
				var startComponentMessages = _consumer.HandledMessages.OfType<ProjectionSubsystemMessage.StartComponents>().First();
				var startCoreMessages = _consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.StartCore>();

				Assert.AreEqual(1, startCoreMessages.Select(x => x.InstanceCorrelationId).Distinct().Count());
				Assert.AreEqual(startComponentMessages.InstanceCorrelationId, startCoreMessages.First().InstanceCorrelationId);
			}
		}

		[TestFixture]
		public class when_starting_as_slave : with_projections_subsystem {
			protected override IEnumerable<WhenStep> PreWhen() {
				yield return (new SystemMessage.BecomeSlave(Guid.NewGuid(),
					new EventStore.Core.Data.VNodeInfo(Guid.NewGuid(), 1,
						new IPEndPoint(IPAddress.Loopback, 1111),
						new IPEndPoint(IPAddress.Loopback, 1112),
						new IPEndPoint(IPAddress.Loopback, 1113),
						new IPEndPoint(IPAddress.Loopback, 1114),
						new IPEndPoint(IPAddress.Loopback, 1115),
						new IPEndPoint(IPAddress.Loopback, 1116),
						false
					)));
				yield return (new SystemMessage.SystemCoreReady());
				yield return Yield;
				if (_startSystemProjections) {
					yield return
						new ProjectionManagementMessage.Command.GetStatistics(Envelope, ProjectionMode.AllNonTransient,
							null, false)
						;
					var statistics = HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Last();
					foreach (var projection in statistics.Projections) {
						if (projection.Status != "Running")
							yield return
								new ProjectionManagementMessage.Command.Enable(
									Envelope, projection.Name, ProjectionManagementMessage.RunAs.Anonymous);
					}
				}
			}

			[Test]
			public void projections_core_coordinator_should_not_publish_start_core_message() {
				//projections are not allowed (yet) to run on slaves
				var startCoreMessages = _consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.StartCore>();
				Assert.AreEqual(0, startCoreMessages.Select(x => x.InstanceCorrelationId).Distinct().Count());
			}
		}
	}
}
