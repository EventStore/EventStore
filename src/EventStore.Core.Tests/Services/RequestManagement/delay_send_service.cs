using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RequestManagement {
	public class delay_send_service {
		private class TimeTravelPublisher : IPublisher {
			public readonly List<(Message message, long time)> Messages;
			public TimeTravelPublisher() => Messages = new List<(Message, long)>();
			public void Publish(Message message) => Messages.Add((message, DateTime.UtcNow.Ticks));
		}

		private class TestMessage : Message {
			public int Id { get; }
			public TestMessage(int id) => Id = id;
		}

		private DelaySendService _sut;
		private TimeTravelPublisher _publisher;
		private readonly int ToleranceMs = Stopwatch.IsHighResolution ? 1 : 20;

		[SetUp]
		public void SetUp() {
			_publisher = new TimeTravelPublisher();
			_sut = new DelaySendService(_publisher);
		}

		[Test]
		public async Task message_is_published_after_specified_delay() {
			const int delayMs = 100;
			var start = DateTime.UtcNow.Ticks;
			_sut.DelaySend(TimeSpan.FromMilliseconds(delayMs), new TestMessage(0));
			await Task.Delay(delayMs * 2);

			Assert.AreEqual(1, _publisher.Messages.Count);

			var elapsedMs = (_publisher.Messages[0].time - start) / TimeSpan.TicksPerMillisecond;
			Assert.GreaterOrEqual(elapsedMs, delayMs);
			Assert.LessOrEqual(elapsedMs, delayMs + ToleranceMs);
		}


		[Test]
		public async Task messages_are_published_after_specified_delays() {
			var start = DateTime.UtcNow.Ticks;

			_sut.DelaySend(TimeSpan.FromMilliseconds(333), new TestMessage(2));
			_sut.DelaySend(TimeSpan.FromMilliseconds(222), new TestMessage(1));
			_sut.DelaySend(TimeSpan.FromMilliseconds(111), new TestMessage(0));

			await Task.Delay(400);

			Assert.AreEqual(3, _publisher.Messages.Count);
			Assert.AreEqual(0, ((TestMessage)_publisher.Messages[0].message).Id);
			Assert.AreEqual(1, ((TestMessage)_publisher.Messages[1].message).Id);
			Assert.AreEqual(2, ((TestMessage)_publisher.Messages[2].message).Id);

			var t1 = (_publisher.Messages[0].time - start) / TimeSpan.TicksPerMillisecond;
			var t2 = (_publisher.Messages[1].time - start) / TimeSpan.TicksPerMillisecond;
			var t3 = (_publisher.Messages[2].time - start) / TimeSpan.TicksPerMillisecond;

			Assert.GreaterOrEqual(t1, 111);
			Assert.LessOrEqual(t1, 111 + ToleranceMs);

			Assert.GreaterOrEqual(t2, 222);
			Assert.LessOrEqual(t2, 222 + ToleranceMs);

			Assert.GreaterOrEqual(t3, 333);
			Assert.LessOrEqual(t3, 333 + ToleranceMs);
		}

		[TearDown]
		public void TearDown() => _sut?.Dispose();
	}
}
