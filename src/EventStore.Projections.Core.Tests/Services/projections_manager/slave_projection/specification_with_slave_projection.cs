using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.slave_projection {
	public abstract class specification_with_slave_projection : TestFixtureWithProjectionCoreAndManagementServices {
		protected Guid _coreProjectionCorrelationId;
		protected Guid _masterWorkerId;

		protected override void Given() {
			base.Given();
			_coreProjectionCorrelationId = Guid.NewGuid();
			_masterWorkerId = Guid.NewGuid();
			AllWritesSucceed();
			NoOtherStreams();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
			yield return
				new CoreProjectionManagementMessage.CreateAndPrepareSlave(
					_coreProjectionCorrelationId,
					Guid.NewGuid(),
					"projection",
					new ProjectionVersion(1, 0, 0),
					new ProjectionConfig(
						SystemAccount.Principal,
						0,
						0,
						1000,
						1000,
						false,
						false,
						false,
						true,
						true,
						true,
						10000,
						1),
					_masterWorkerId,
					_coreProjectionCorrelationId,
					//(handlerType, query) => new FakeProjectionStateHandler(
					//    configureBuilder: builder =>
					//    {
					//        builder.FromCatalogStream("catalog");
					//        builder.AllEvents();
					//        builder.SetByStream();
					//        builder.SetLimitingCommitPosition(10000);
					//    }),
					typeof(FakeProjectionStateHandler).GetNativeHandlerName(),
					"");
			yield return Yield;
		}
	}

	[TestFixture]
	public class when_creating_a_slave_projection : specification_with_slave_projection {
		protected override IEnumerable<WhenStep> When() {
			foreach (var m in base.When()) yield return m;
			yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId, Guid.NewGuid());
		}

		[Test]
		public void replies_with_slave_projection_reader_id_on_started_message() {
			var readerAssigned =
				HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();

			Assert.IsNotNull(readerAssigned);
			Assert.AreNotEqual(Guid.Empty, readerAssigned.SubscriptionId);
		}
	}

	[TestFixture]
	public class when_processing_a_stream : specification_with_slave_projection {
		private Guid _subscriptionId;

		protected override void Given() {
			base.Given();
			ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var m in base.When()) yield return m;
			yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId, Guid.NewGuid());
			var readerAssigned =
				HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
			Assert.IsNotNull(readerAssigned);
			_subscriptionId = readerAssigned.SubscriptionId;
			yield return
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
					"test-stream",
					0,
					10000);
		}

		[Test]
		public void publishes_partition_result_message() {
			var results =
				HandledMessages.OfType<PartitionProcessingResultOutput>().ToArray();
			Assert.AreEqual(1, results.Length);
			var result = results[0];
			Assert.AreEqual("{\"data\":1}", result.Result);
		}
	}

	[TestFixture]
	public class when_processing_a_non_existing_stream : specification_with_slave_projection {
		private Guid _subscriptionId;

		protected override void Given() {
			base.Given();
			// No streams
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var m in base.When()) yield return m;
			yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId, Guid.NewGuid());
			var readerAssigned =
				HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
			Assert.IsNotNull(readerAssigned);
			_subscriptionId = readerAssigned.SubscriptionId;
			yield return
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
					"test-stream",
					0,
					10000);
		}

		[Test]
		public void publishes_partition_result_message() {
			var results =
				HandledMessages.OfType<PartitionProcessingResultOutput>().ToArray();
			Assert.AreEqual(1, results.Length);
			var result = results[0];
			Assert.IsNull(result.Result);
		}
	}

	[TestFixture]
	public class when_processing_multiple_streams : specification_with_slave_projection {
		private Guid _subscriptionId;

		protected override void Given() {
			base.Given();
			ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
			ExistingEvent("test-stream", "handle_this_type", "", "{\"data\":1}");
			ExistingEvent("test-stream2", "handle_this_type", "", "{\"data\":2}");
		}

		protected override IEnumerable<WhenStep> When() {
			foreach (var m in base.When()) yield return m;
			yield return new CoreProjectionManagementMessage.Start(_coreProjectionCorrelationId, Guid.NewGuid());
			var readerAssigned =
				HandledMessages.OfType<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>().LastOrDefault();
			Assert.IsNotNull(readerAssigned);
			_subscriptionId = readerAssigned.SubscriptionId;
			yield return
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
					"test-stream",
					0,
					10000);
			yield return
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
					"test-stream2",
					1,
					10000);
			yield return
				new ReaderSubscriptionManagement.SpoolStreamReadingCore(_subscriptionId,
					"test-stream3",
					2,
					10000);
		}

		[Test]
		public void publishes_partition_result_message() {
			var results =
				HandledMessages.OfType<PartitionProcessingResultOutput>().ToArray();
			Assert.AreEqual(3, results.Length);
			Assert.AreEqual("{\"data\":1}", results[0].Result);
			Assert.AreEqual("{\"data\":2}", results[1].Result);
			Assert.IsNull(results[2].Result);
		}
	}
}
