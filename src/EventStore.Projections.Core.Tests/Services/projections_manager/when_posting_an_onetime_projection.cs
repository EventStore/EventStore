using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	[TestFixture]
	public class when_posting_an_onetime_projection : TestFixtureWithProjectionCoreAndManagementServices {
		protected override void Given() {
			NoOtherStreams();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return (new SystemMessage.BecomeMaster(Guid.NewGuid()));
			yield return (new SystemMessage.EpochWritten(new EpochRecord(0L, 0, Guid.NewGuid(), 0L, DateTime.Now)));
			yield return (new SystemMessage.SystemCoreReady());
			yield return
				(new ProjectionManagementMessage.Command.Post(
					new PublishEnvelope(_bus), ProjectionManagementMessage.RunAs.Anonymous,
					@"fromAll().when({$any:function(s,e){return s;}});", enabled: true));
		}

		[Test, Category("v8")]
		public void projection_updated_is_published() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Updated>().Count());
		}
	}
}
