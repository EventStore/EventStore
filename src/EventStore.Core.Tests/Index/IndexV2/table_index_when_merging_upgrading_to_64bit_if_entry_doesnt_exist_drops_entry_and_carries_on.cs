using System;
using System.Linq;
using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV2 {
	[TestFixture, Category("LongRunning")]
	public class
		table_index_when_merging_upgrading_to_64bit_if_single_stream_entry_doesnt_exist_drops_entry_and_carries_on :
			SpecificationWithDirectoryPerTestFixture {
		private TableIndex _tableIndex;
		private IHasher _lowHasher;
		private IHasher _highHasher;
		private string _indexDir;
		protected byte _ptableVersion;

		// Note hash is by length so stream ids are set to order them specifically in the index.
		private const string Stream1 = "hhh";
		private const string Stream2 = "hh";
		private const string Stream3 = "h";

		public
			table_index_when_merging_upgrading_to_64bit_if_single_stream_entry_doesnt_exist_drops_entry_and_carries_on() {
			_ptableVersion = PTableVersions.IndexV2;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			_indexDir = PathName;
			var fakeReader = new TFReaderLease(new FakeIndexReader2());
			_lowHasher = new ByLengthHasher();
			_highHasher = new ByLengthHasher();
			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(PTableVersions.IndexV1, maxSize: 3),
				() => fakeReader,
				PTableVersions.IndexV1,
				5,
				maxSizeForMemory: 3,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);

			_tableIndex.Add(1, Stream1, 0, 1);
			_tableIndex.Add(1, Stream2, 0, 2);
			_tableIndex.Add(1, Stream3, 0, 3);

			_tableIndex.Close(false);

			_tableIndex = new TableIndex(_indexDir, _lowHasher, _highHasher,
				() => new HashListMemTable(_ptableVersion, maxSize: 3),
				() => fakeReader,
				_ptableVersion,
				5,
				maxSizeForMemory: 3,
				maxTablesPerLevel: 2);
			_tableIndex.Initialize(long.MaxValue);

			_tableIndex.Add(1, Stream3, 1, 4);
			_tableIndex.Add(1, Stream2, 1, 5);
			_tableIndex.Add(1, Stream1, 1, 6);

			Thread.Sleep(500);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_tableIndex.Close();

			base.TestFixtureTearDown();
		}

		[Test]
		public void should_have_all_entries_except_scavenged() {
			var streamId = Stream1;
			var result = _tableIndex.GetRange(streamId, 0, 1).ToArray();
			var hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

			Assert.That(result.Count(), Is.EqualTo(1));

			Assert.That(result[0].Stream, Is.EqualTo(hash));
			Assert.That(result[0].Version, Is.EqualTo(1));
			Assert.That(result[0].Position, Is.EqualTo(6));

			streamId = Stream2;
			result = _tableIndex.GetRange(streamId, 0, 1).ToArray();
			hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

			Assert.That(result.Count(), Is.EqualTo(1));

			Assert.That(result[0].Stream, Is.EqualTo(hash));
			Assert.That(result[0].Version, Is.EqualTo(1));
			Assert.That(result[0].Position, Is.EqualTo(5));

			streamId = Stream3;
			result = _tableIndex.GetRange(streamId, 0, 1).ToArray();
			hash = (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);

			Assert.That(result.Count(), Is.EqualTo(2));

			Assert.That(result[0].Stream, Is.EqualTo(hash));
			Assert.That(result[0].Version, Is.EqualTo(1));
			Assert.That(result[0].Position, Is.EqualTo(4));

			Assert.That(result[1].Stream, Is.EqualTo(hash));
			Assert.That(result[1].Version, Is.EqualTo(0));
			Assert.That(result[1].Position, Is.EqualTo(3));
		}

		private class FakeIndexReader2 : ITransactionFileReader {
			public void Reposition(long position) {
				throw new NotImplementedException();
			}

			public SeqReadResult TryReadNext() {
				throw new NotImplementedException();
			}

			public SeqReadResult TryReadPrev() {
				throw new NotImplementedException();
			}

			public RecordReadResult TryReadAt(long position) {
				string eventStreamId;
				switch (position) {
					case 1:
						eventStreamId = Stream1;
						break;
					case 2:
						eventStreamId = Stream2;
						break;
					case 3:
						eventStreamId = Stream3;
						break;
					default:
						throw new ArgumentOutOfRangeException("Unexpected position look up.");
				}

				var record = (LogRecord)new PrepareLogRecord(position, Guid.NewGuid(), Guid.NewGuid(), 0, 0,
					eventStreamId, -1, DateTime.UtcNow, PrepareFlags.None, "type", new byte[0], null);
				return new RecordReadResult(true, position + 1, record, 1);
			}

			public bool ExistsAt(long position) {
				return position != 2 && position != 1;
			}
		}
	}
}
