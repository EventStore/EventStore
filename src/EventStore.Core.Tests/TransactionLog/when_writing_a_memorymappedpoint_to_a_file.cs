using System;
using System.Threading;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_writing_a_memorymappedpoint_to_a_file : SpecificationWithFile {
		[Test]
		public void a_null_file_throws_argumentnullexception() {
			Assert.Throws<ArgumentNullException>(() => new MemoryMappedFileCheckpoint(null));
		}

		[Test]
		public void name_is_set() {
			var checksum = new MemoryMappedFileCheckpoint(Filename, "test", false);
			Assert.AreEqual("test", checksum.Name);
			checksum.Close();
		}

		[Test]
		public void reading_off_same_instance_gives_most_up_to_date_info() {
			var checkSum = new MemoryMappedFileCheckpoint(Filename);
			checkSum.Write(0xDEAD);
			checkSum.Flush();
			var read = checkSum.Read();
			checkSum.Close();
			Assert.AreEqual(0xDEAD, read);
		}

		[Test]
		public void can_read_existing_checksum() {
			var checksum = new MemoryMappedFileCheckpoint(Filename);
			checksum.Write(0xDEAD);
			checksum.Close();
			checksum = new MemoryMappedFileCheckpoint(Filename);
			var val = checksum.Read();
			checksum.Close();
			Assert.AreEqual(0xDEAD, val);
		}

		[Test]
		public void the_new_value_is_not_accessible_if_not_flushed_even_with_delay() {
			var checkSum = new MemoryMappedFileCheckpoint(Filename);
			var readChecksum = new MemoryMappedFileCheckpoint(Filename);
			checkSum.Write(1011);
			Thread.Sleep(200);
			Assert.AreEqual(0, readChecksum.Read());
			checkSum.Close();
			readChecksum.Close();
		}

		[Test]
		public void the_new_value_is_accessible_after_flush() {
			var checkSum = new MemoryMappedFileCheckpoint(Filename);
			var readChecksum = new MemoryMappedFileCheckpoint(Filename);
			checkSum.Write(1011);
			checkSum.Flush();
			Assert.AreEqual(1011, readChecksum.Read());
			checkSum.Close();
			readChecksum.Close();
			Thread.Sleep(100);
		}
	}
}
