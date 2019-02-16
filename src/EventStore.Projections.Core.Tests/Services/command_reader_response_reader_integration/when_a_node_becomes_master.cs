using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.command_reader_response_reader_integration {
	[TestFixture]
	public class when_a_node_becomes_master : TestFixtureWithProjectionCoreAndManagementServices {
		private Guid _uniqueId;

		protected override void Given() {
			AllWritesSucceed();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new SystemMessage.BecomeMaster(Guid.NewGuid());

			_uniqueId = Guid.NewGuid();
			EpochRecord epochRecord = new EpochRecord(0L, 0, _uniqueId, 0L, DateTime.Now);

			yield return new SystemMessage.EpochWritten(epochRecord);
			yield return new SystemMessage.SystemCoreReady();
		}

		[Test]
		public void readers_should_use_the_same_control_stream_id() {
			Assert.AreEqual(_uniqueId,
				_consumer.HandledMessages.OfType<ProjectionManagementMessage.Starting>().First().EpochId);
			Assert.AreEqual(_uniqueId,
				_consumer.HandledMessages.OfType<ProjectionCoreServiceMessage.StartCore>().First().EpochId);
		}
	}
}
