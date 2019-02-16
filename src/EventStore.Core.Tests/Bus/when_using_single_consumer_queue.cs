using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus {
	[TestFixture]
	public abstract class when_using_single_consumer_queue {
		readonly Func<int, ISingleConsumerMessageQueue> _factory;
		readonly int _numberOfProducers;

		protected when_using_single_consumer_queue(Func<int, ISingleConsumerMessageQueue> factory,
			int numberOfProducers) {
			_factory = factory;
			_numberOfProducers = numberOfProducers;
		}

		[Test]
		public void messages_should_be_dispatched_in_fifo_way() {
			const int messagesToSendPerThread = 1 << 16;
			var queue = _factory(messagesToSendPerThread);

			var startEvent = new ManualResetEventSlim(false);
			var producers = new Thread[_numberOfProducers];
			var endEvent = new CountdownEvent(producers.Length);

			var messages = new Message[] {
				new SystemMessage.SystemInit(),
				new SystemMessage.SystemInit(),
				new SystemMessage.SystemInit(),
				new SystemMessage.SystemInit(),
			};

			for (var i = 0; i < producers.Length; i++) {
				producers[i] = new Thread(msgs => {
					var toSend = (Message[])msgs;
					startEvent.Wait();
					for (var j = 0; j < messagesToSendPerThread; j++) {
						// start with second message
						queue.Enqueue(toSend[(j + 1) % 2]);
					}

					endEvent.Signal();
				}) {
					IsBackground = true,
					Name = "Producer #" + i
				};
				producers[i].Start(messages.Skip(i * 2).Take(2).ToArray());
			}

			var batch = new Message[1024];
			var producerCount = new int[producers.Length];
			var expectedMessages = messagesToSendPerThread * producers.Length;
			startEvent.Set();
			var count = 0;
			while (count < expectedMessages) {
				QueueBatchDequeueResult result;
				if (queue.TryDequeue(batch, out result)) {
					for (var i = 0; i < result.DequeueCount; i++) {
						var index = Array.IndexOf(messages, batch[i]);
						var producerIndex = index >> 1;
						var messageIndex = index & 1;

						Assert.AreNotEqual(producerCount[producerIndex], messageIndex);
						producerCount[producerIndex] = messageIndex;
					}

					count += result.DequeueCount;
				}
			}

			Assert.True(endEvent.Wait(TimeSpan.FromSeconds(10)));
		}
	}

	[TestFixture]
	class when_using_single_consumer_queue_multiple_producers : when_using_single_consumer_queue {
		public when_using_single_consumer_queue_multiple_producers() : base(size => new MPSCMessageQueue(size), 2) {
		}
	}

	[TestFixture]
	class when_using_single_consumer_queue_single_producers : when_using_single_consumer_queue {
		public when_using_single_consumer_queue_single_producers() : base(size => new SPSCMessageQueue(size), 1) {
		}
	}
}
