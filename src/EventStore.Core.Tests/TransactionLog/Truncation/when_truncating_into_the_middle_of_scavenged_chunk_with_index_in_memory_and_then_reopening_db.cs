using System.IO;
using System.Linq;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		when_truncating_into_the_middle_of_scavenged_chunk_with_index_in_memory_and_then_reopening_db<TLogFormat, TStreamId> :
			TruncateAndReOpenDbScenario<TLogFormat, TStreamId> {
		// actually this case is not fully handled by EventStore. When some records have been scavenged, but truncated in a middle of chunk,
		// we lose the delete record, so we don't consider this stream as deleted, but still we have scavenged a lot of records, that are not scavenged in other replicas.
		// nonetheless this scenario is very very unlikely to happen, that's why it is not handled. if it still does happen - a whole db on truncated node should be deleted

		private string _chunk0;
		private string _chunk1;
		private string _chunk2;
		private string _chunk3;
		private EventRecord _event2;
		private EventRecord _chunkEdge;

		protected override void WriteTestScenario() {
			WriteSingleEvent("ES1", 0, new string('.', 3000)); // chunk 0
			WriteSingleEvent("ES1", 1, new string('.', 3000));
			_event2 = WriteSingleEvent("ES2", 0, new string('.', 3000));
			_chunkEdge = WriteSingleEvent("ES1", 2, new string('.', 3000), retryOnFail: true); // chunk 1
			var rec = WriteSingleEvent("ES1", 3, new string('.', 3000));
			WriteSingleEvent("ES1", 4, new string('.', 3000));
			WriteSingleEvent("ES1", 5, new string('.', 3000), retryOnFail: true); // chunk 2
			WriteSingleEvent("ES1", 6, new string('.', 3000));
			WriteSingleEvent("ES1", 7, new string('.', 3000));
			WriteSingleEvent("ES1", 8, new string('.', 3000), retryOnFail: true); // chunk 3

			WriteDelete("ES1");
			Scavenge(completeLast: false, mergeChunks: false);

			TruncateCheckpoint = rec.LogPosition;
		}

		protected override void OnBeforeTruncating() {
			// scavenged chunk names
			// TODO MM: avoid this complexity - try scavenging exactly at where its invoked and not wait for readIndex to rebuild
			_chunk0 = GetChunkName(0);
			_chunk1 = GetChunkName(1);
			_chunk2 = GetChunkName(2);
			_chunk3 = GetChunkName(3);

			Assert.IsTrue(File.Exists(_chunk0));
			Assert.IsTrue(File.Exists(_chunk1));
			Assert.IsTrue(File.Exists(_chunk2));
			Assert.IsTrue(File.Exists(_chunk3));
		}

		private string GetChunkName(int chunkNumber) {
			var allVersions = Db.Config.FileNamingStrategy.GetAllVersionsFor(chunkNumber);
			Assert.AreEqual(1, allVersions.Length);
			return allVersions[0];
		}

		[Test]
		public void truncated_chunks_should_be_deleted() {
			Assert.IsFalse(File.Exists(_chunk2));
			Assert.IsFalse(File.Exists(_chunk3));
		}

		[Test]
		public void intersecting_chunk_should_be_deleted() {
			Assert.IsFalse(File.Exists(_chunk1));
		}

		[Test]
		public void untouched_chunk_should_survive() {
			Assert.AreEqual(_chunk0, GetChunkName(0));
		}

		[Test]
		public void checksums_should_be_equal_to_beginning_of_intersected_scavenged_chunk() {
			Assert.AreEqual(_chunkEdge.TransactionPosition, WriterCheckpoint.Read());
			Assert.AreEqual(_chunkEdge.TransactionPosition, ChaserCheckpoint.Read());
		}

		[Test]
		public void read_one_by_one_returns_survived_records() {
			var res = ReadIndex.ReadEvent("ES2", 0);
			Assert.AreEqual(ReadEventResult.Success, res.Result);
			Assert.AreEqual(_event2, res.Record);
		}

		[Test]
		public void there_is_no_previously_scavenged_stream_which_delete_record_was_truncated() {
			var res = ReadIndex.ReadEvent("ES1", 0);
			Assert.AreEqual(ReadEventResult.NoStream, res.Result);
			Assert.IsNull(res.Record);
		}

		[Test]
		public void read_stream_forward_doesnt_return_untouched_records() {
			var res = ReadIndex.ReadStreamEventsForward("ES2", 0, 100);
			var records = res.Records;
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event2, records[0]);
		}

		[Test]
		public void read_stream_forward_doesnt_return_truncated_or_scavenged_records_but_returns_stream_created() {
			var res = ReadIndex.ReadStreamEventsForward("ES1", 0, 100);
			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}

		[Test]
		public void read_stream_backward_doesnt_return_untoucned_records() {
			var res = ReadIndex.ReadStreamEventsBackward("ES2", -1, 100);
			var records = res.Records;
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event2, records[0]);
		}

		[Test]
		public void read_stream_backward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadStreamEventsBackward("ES1", -1, 100);
			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}

		[Test]
		public void read_all_forward_returns_only_survived_events() {
			var res = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100);
			var records = res.Records.Select(r => r.Event).ToArray();

			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event2, records[0]);
		}

		[Test]
		public void read_all_backward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100);
			var records = res.Records.Select(r => r.Event).ToArray();

			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event2, records[0]);
		}
	}
}
