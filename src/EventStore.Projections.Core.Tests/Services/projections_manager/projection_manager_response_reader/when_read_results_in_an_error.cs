using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_read_results_in_an_error : specification_with_projection_manager_response_reader_started {
		private Guid _projectionId;
		private Guid _readStreamEventsCorrelationId;
		private string _projectionsMasterStream = "$projections-$master";

		protected override IEnumerable<WhenStep> When() {
			AllReadsTimeOut();
			_consumer.HandledMessages.Clear();

			_projectionId = Guid.NewGuid();
			yield return
				CreateWriteEvent(
					_projectionsMasterStream,
					"$stopped",
					@"{
                        ""id"":""" + _projectionId.ToString("N") + @""",
                    }",
					null,
					true);
			var readStreamMessage = _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
				.LastOrDefault(x => x.EventStreamId == _projectionsMasterStream);
			Assert.IsNotNull(readStreamMessage, "Initial read was not issued");

			_readStreamEventsCorrelationId = readStreamMessage.CorrelationId;
			readStreamMessage.Envelope.ReplyWith(new ClientMessage.ReadStreamEventsForwardCompleted(
				_readStreamEventsCorrelationId, _projectionsMasterStream, -1, -1,
				ReadStreamResult.Error, new ResolvedEvent[0], null, false, "Test error while reading stream",
				-1, -1, true, 0));
		}

		[Test]
		public void issues_a_new_read() {
			var response = HandledMessages.OfType<ClientMessage.ReadStreamEventsForward>()
				.Last(x => x.EventStreamId == _projectionsMasterStream);
			Assert.IsNotNull(response);
			Assert.AreNotEqual(_readStreamEventsCorrelationId, response.CorrelationId);
		}
	}
}
