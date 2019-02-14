using System;
using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using System.Linq;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Projections.Core.Tests.Services.projections_system {
	public abstract class with_projections_subsystem : TestFixtureWithProjectionCoreAndManagementServices {
		protected bool _startSystemProjections;

		protected override bool GivenInitializeSystemProjections() {
			return true;
		}

		protected override void Given1() {
			base.Given1();
			_startSystemProjections = GivenStartSystemProjections();
			AllWritesSucceed();
			NoOtherStreams();
			EnableReadAll();
		}

		protected virtual bool GivenStartSystemProjections() {
			return false;
		}

		protected override IEnumerable<WhenStep> PreWhen() {
			yield return (new SystemMessage.BecomeMaster(Guid.NewGuid()));
			yield return (new SystemMessage.EpochWritten(new EpochRecord(0L, 0, Guid.NewGuid(), 0L, DateTime.Now)));
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
	}
}
