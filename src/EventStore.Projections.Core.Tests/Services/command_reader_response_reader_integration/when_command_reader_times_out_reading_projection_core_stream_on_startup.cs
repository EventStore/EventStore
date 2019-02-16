using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.command_reader_response_reader_integration {
	[TestFixture]
	public class
		when_command_reader_times_out_reading_projection_core_stream_on_startup :
			specification_with_command_reader_and_response_reader {
		private Guid _epochId = Guid.NewGuid();

		protected override void Given() {
			_coreServiceId = Guid.NewGuid().ToString("N");
			_numberOfWorkers = 1;

			var projectionCoreStream = "$projections-$" + _coreServiceId;
			TimeOutReadToStreamOnce(projectionCoreStream);

			var hasTimedOut = false;
			_bus.Subscribe(new AdHocHandler<TimerMessage.Schedule>(msg => {
				var delay = msg.ReplyMessage as IODispatcherDelayedMessage;
				if (delay != null && !hasTimedOut) {
					hasTimedOut = true;
					msg.Reply();
				}
			}));

			// Start up reader
			base.Given();
		}

		protected override IEnumerable<WhenStep> When() {
			yield return new WhenStep(
				new ProjectionCoreServiceMessage.StartCore(_epochId),
				new ProjectionManagementMessage.Starting(_epochId));
		}

		[Test]
		public void should_send_reader_ready() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ReaderReady>().Count());
		}
	}
}
