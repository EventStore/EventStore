using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.master_core_projection_response_reader {
	[TestFixture]
	public class when_response_reader_starts_up_successfully : with_master_core_response_reader,
		IHandle<PartitionProcessingResult> {
		public ManualResetEventSlim _mre = new ManualResetEventSlim();

		[OneTimeSetUp]
		protected override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_bus.Subscribe<PartitionProcessingResult>(this);

			_reader.Start();
		}

		public override void Handle(ClientMessage.ReadStreamEventsForward message) {
			message.Envelope.ReplyWith(CreateResultCommandReadResponse(message));
		}

		public void Handle(PartitionProcessingResult message) {
			_mre.Set();
		}

		[Test]
		public void should_publish_command() {
			Assert.IsTrue(_mre.Wait(10000));
		}
	}
}
