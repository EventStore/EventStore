using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.PersistentSubscription {
	[TestFixture]
	public class StreamBufferTests {
		[Test]
		public void adding_read_message_in_correct_order() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id, 0));
			Assert.AreEqual(1, buffer.BufferCount);
			OutstandingMessage message = buffer.Scan().First().Message;
			Assert.AreEqual(id, message.EventId);
			Assert.IsFalse(buffer.Live);
		}

		[Test]
		public void adding_multiple_read_message_in_correct_order() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			buffer.AddReadMessage(BuildMessageAt(id2, 1));
			Assert.AreEqual(2, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(1, buffer.BufferCount);
			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
			Assert.IsFalse(buffer.Live);
		}


		[Test]
		public void adding_multiple_read_message_in_wrong_order() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 1));
			buffer.AddReadMessage(BuildMessageAt(id2, 0));
			Assert.AreEqual(2, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(1, buffer.BufferCount);
			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
			Assert.IsFalse(buffer.Live);
		}

		[Test]
		public void adding_multiple_same_read_message() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			Assert.AreEqual(2, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(1, buffer.BufferCount);
			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
			Assert.IsFalse(buffer.Live);
		}

		[Test]
		public void adding_messages_to_read_after_same_on_live_switches_to_live() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			buffer.AddLiveMessage(BuildMessageAt(id1, 0));
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			Assert.IsTrue(buffer.Live);
			Assert.AreEqual(1, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
		}

		[Test]
		public void adding_messages_to_read_after_later_live_does_not_switch() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			buffer.AddLiveMessage(BuildMessageAt(id1, 5));
			buffer.AddReadMessage(BuildMessageAt(id2, 0));
			Assert.IsFalse(buffer.Live);
			Assert.AreEqual(1, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
		}

		[Test]
		public void adding_messages_to_live_without_start_from_beginning() {
			var buffer = new StreamBuffer(10, 10, -1, false);
			var id1 = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			buffer.AddLiveMessage(BuildMessageAt(id1, 6));
			buffer.AddLiveMessage(BuildMessageAt(id2, 7));
			Assert.IsTrue(buffer.Live);
			Assert.AreEqual(2, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(1, buffer.BufferCount);
			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
		}

		[Test]
		public void adding_messages_with_lower_in_live() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			var id2 = Guid.NewGuid();
			buffer.AddLiveMessage(BuildMessageAt(id1, 5));
			buffer.AddLiveMessage(BuildMessageAt(id1, 6));
			buffer.AddLiveMessage(BuildMessageAt(id2, 7));
			buffer.AddReadMessage(BuildMessageAt(id1, 7));
			Assert.IsTrue(buffer.Live);
			Assert.AreEqual(1, buffer.BufferCount);
			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
		}

		[Test]
		public void skipped_messages_are_not_removed() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			var id2 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id2, 1));
			var id3 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id3, 2));
			Assert.AreEqual(3, buffer.BufferCount);

			var messagePointer2 = buffer.Scan().Skip(1).First(); // Skip the first message.
			Assert.AreEqual(id2, messagePointer2.Message.EventId);
			messagePointer2.MarkSent();
			Assert.AreEqual(2, buffer.BufferCount);

			var messagePointers = buffer.Scan().ToArray();
			Assert.AreEqual(id1, messagePointers[0].Message.EventId);
			Assert.AreEqual(id3, messagePointers[1].Message.EventId);
		}

		[Test]
		public void retried_messages_appear_first() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			var id2 = Guid.NewGuid();
			buffer.AddRetry(BuildMessageAt(id2, 2));
			Assert.AreEqual(2, buffer.BufferCount);
			Assert.AreEqual(1, buffer.RetryBufferCount);
			Assert.AreEqual(1, buffer.ReadBufferCount);

			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(1, buffer.BufferCount);
			Assert.AreEqual(0, buffer.RetryBufferCount);
			Assert.AreEqual(1, buffer.ReadBufferCount);

			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id1, messagePointer.Message.EventId);
			messagePointer.MarkSent();
			Assert.AreEqual(0, buffer.BufferCount);
			Assert.AreEqual(0, buffer.RetryBufferCount);
			Assert.AreEqual(0, buffer.ReadBufferCount);

			Assert.IsFalse(buffer.Live);
		}

		[Test]
		public void retried_messages_appear_in_version_order() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			var id1 = Guid.NewGuid();
			buffer.AddReadMessage(BuildMessageAt(id1, 0));
			var id2 = Guid.NewGuid();
			var id3 = Guid.NewGuid();
			var id4 = Guid.NewGuid();
			var id5 = Guid.NewGuid();
			buffer.AddRetry(BuildMessageAt(id2, 2));
			buffer.AddRetry(BuildMessageAt(id3, 3));
			buffer.AddRetry(BuildMessageAt(id4, 1));
			buffer.AddRetry(BuildMessageAt(id5, 2));

			var messagePointer = buffer.Scan().First();
			Assert.AreEqual(id4, messagePointer.Message.EventId);
			messagePointer.MarkSent();

			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id2, messagePointer.Message.EventId);
			messagePointer.MarkSent();

			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id5, messagePointer.Message.EventId);
			messagePointer.MarkSent();

			messagePointer = buffer.Scan().First();
			Assert.AreEqual(id3, messagePointer.Message.EventId);
			messagePointer.MarkSent();
		}

		public void lowest_retry_doesnt_assume_order() {
			var buffer = new StreamBuffer(10, 10, -1, true);
			buffer.AddRetry(BuildMessageAt(Guid.NewGuid(), 4));
			buffer.AddRetry(BuildMessageAt(Guid.NewGuid(), 2));
			buffer.AddRetry(BuildMessageAt(Guid.NewGuid(), 3));
			Assert.AreEqual(2, buffer.GetLowestRetry());
		}

		private OutstandingMessage BuildMessageAt(Guid id, int version) {
			return new OutstandingMessage(id, null, BuildEventAt(id, version), 0);
		}

		private ResolvedEvent BuildEventAt(Guid id, int version) {
			return Helper.BuildFakeEvent(id, "foo", "bar", version);
		}
	}
}
