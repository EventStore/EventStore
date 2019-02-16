using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class
		when_receiving_spool_stream_reading_command :
			specification_with_projection_core_service_command_reader_started {
		protected override IEnumerable<WhenStep> When() {
			yield return
				CreateWriteEvent(
					"$projections-$" + _serviceId,
					"$spool-stream-reading",
					@"{
                        ""subscriptionId"":""4fb6aa53932045ce891752441a0fde5c"",
                         ""streamId"":""streamId"",
                         ""catalogSequenceNumber"":100,
                         ""limitingCommitposition"":123456789123456789,
                    }",
					null,
					true);
		}

		[Test]
		public void publishesspool_stream_reading_message() {
			var spoolStreamReading =
				HandledMessages.OfType<ReaderSubscriptionManagement.SpoolStreamReadingCore>().LastOrDefault();
			Assert.IsNotNull(spoolStreamReading);
			Assert.AreEqual(Guid.ParseExact("4fb6aa53932045ce891752441a0fde5c", "N"),
				spoolStreamReading.SubscriptionId);
			Assert.AreEqual("streamId", spoolStreamReading.StreamId);
			Assert.AreEqual(100, spoolStreamReading.CatalogSequenceNumber);
			Assert.AreEqual(123456789123456789, spoolStreamReading.LimitingCommitPosition);
		}
	}
}
