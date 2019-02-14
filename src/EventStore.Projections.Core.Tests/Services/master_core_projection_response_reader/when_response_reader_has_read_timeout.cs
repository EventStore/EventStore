using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.master_core_projection_response_reader {
	[TestFixture]
	public class when_response_reader_has_read_timeout : with_master_core_response_reader,
		IHandle<PartitionProcessingResult>,
		IHandle<TimerMessage.Schedule> {
		private bool _hasTimedOut;
		public ManualResetEventSlim _mre = new ManualResetEventSlim();

		[OneTimeSetUp]
		protected override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_bus.Subscribe<PartitionProcessingResult>(this);
			_bus.Subscribe<TimerMessage.Schedule>(this);

			_reader.Start();
		}

		public void Handle(TimerMessage.Schedule message) {
			if (!_hasTimedOut && message.ReplyMessage as IODispatcherDelayedMessage != null) {
				_hasTimedOut = true;
				message.Reply();
			}
		}

		public override void Handle(ClientMessage.ReadStreamEventsForward message) {
			if (!_hasTimedOut)
				return;
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
