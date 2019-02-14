using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.command_reader_response_reader_integration {
	public class
		when_command_reader_starts_before_response_reader : specification_with_command_reader_and_response_reader {
		protected override void Given() {
			_numberOfWorkers = 1;
			base.Given();
		}

		protected override IEnumerable<WhenStep> When() {
			var uniqueId = Guid.NewGuid();
			yield return new WhenStep(
				new ProjectionCoreServiceMessage.StartCore(uniqueId),
				new ProjectionManagementMessage.Starting(uniqueId));
		}

		[Test]
		public void should_send_reader_ready() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ReaderReady>().Count());
		}
	}

	public class
		when_command_reader_starts_before_response_reader_with_two_workers :
			specification_with_command_reader_and_response_reader {
		protected override void Given() {
			_numberOfWorkers = 2;
			base.Given();
		}

		protected override IEnumerable<WhenStep> When() {
			var uniqueId = Guid.NewGuid();
			yield return new WhenStep(
				new ProjectionCoreServiceMessage.StartCore(uniqueId),
				new ProjectionCoreServiceMessage.StartCore(uniqueId),
				new ProjectionManagementMessage.Starting(uniqueId));
		}

		[Test]
		public void should_send_reader_ready() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ReaderReady>().Count());
		}
	}

	public class
		when_command_reader_starts_before_response_reader_with_two_workers_one_starting_after_the_response_reader :
			specification_with_command_reader_and_response_reader {
		protected override void Given() {
			_numberOfWorkers = 2;
			base.Given();
		}

		protected override IEnumerable<WhenStep> When() {
			var uniqueId = Guid.NewGuid();
			yield return new WhenStep(
				new ProjectionCoreServiceMessage.StartCore(uniqueId),
				new ProjectionManagementMessage.Starting(uniqueId),
				new ProjectionCoreServiceMessage.StartCore(uniqueId));
		}

		[Test]
		public void should_send_reader_ready() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ReaderReady>().Count());
		}
	}
}
