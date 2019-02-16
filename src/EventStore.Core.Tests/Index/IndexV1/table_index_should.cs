using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Index.IndexV1 {
	[TestFixture(PTableVersions.IndexV1, false)]
	[TestFixture(PTableVersions.IndexV1, true)]
	[TestFixture(PTableVersions.IndexV2, false)]
	[TestFixture(PTableVersions.IndexV2, true)]
	[TestFixture(PTableVersions.IndexV3, false)]
	[TestFixture(PTableVersions.IndexV3, true)]
	[TestFixture(PTableVersions.IndexV4, false)]
	[TestFixture(PTableVersions.IndexV4, true)]
	public class table_index_should : SpecificationWithDirectoryPerTestFixture {
		private TableIndex _tableIndex;
		protected byte _ptableVersion = PTableVersions.IndexV1;
		private bool _skipIndexVerify;

		public table_index_should(byte version, bool skipIndexVerify) {
			_ptableVersion = version;
			_skipIndexVerify = skipIndexVerify;
		}

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			_tableIndex = new TableIndex(PathName, lowHasher, highHasher,
				() => new HashListMemTable(_ptableVersion, maxSize: 20),
				() => { throw new InvalidOperationException(); },
				_ptableVersion,
				5,
				maxSizeForMemory: 10,
				skipIndexVerify: _skipIndexVerify);
			_tableIndex.Initialize(long.MaxValue);
		}

		public override void TestFixtureTearDown() {
			_tableIndex.Close();
			base.TestFixtureTearDown();
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_start_version() {
			Assert.Throws<ArgumentOutOfRangeException>(
				() => _tableIndex.GetRange("0x0000", -1, long.MaxValue).ToArray());
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_range_query_when_provided_with_negative_end_version() {
			Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.GetRange("0x0000", 0, -1).ToArray());
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_get_one_entry_query_when_provided_with_negative_version() {
			long pos;
			Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.TryGetOneValue("0x0000", -1, out pos));
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_commit_position() {
			Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(-1, "0x0000", 0, 0));
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_version() {
			Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, "0x0000", -1, 0));
		}

		[Test]
		public void throw_argumentoutofrangeexception_on_adding_entry_with_negative_position() {
			Assert.Throws<ArgumentOutOfRangeException>(() => _tableIndex.Add(0, "0x0000", 0, -1));
		}
	}
}
