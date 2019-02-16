using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1 {
	[TestFixture(PTableVersions.IndexV1, false)]
	[TestFixture(PTableVersions.IndexV1, true)]
	[TestFixture(PTableVersions.IndexV2, false)]
	[TestFixture(PTableVersions.IndexV2, true)]
	[TestFixture(PTableVersions.IndexV3, false)]
	[TestFixture(PTableVersions.IndexV3, true)]
	[TestFixture(PTableVersions.IndexV4, false)]
	[TestFixture(PTableVersions.IndexV4, true)]
	public class ptable_range_query_tests : SpecificationWithFilePerTestFixture {
		protected byte _ptableVersion = PTableVersions.IndexV1;
		private PTable _ptable;
		private bool _skipIndexVerify;

		public ptable_range_query_tests(byte version, bool skipIndexVerify) {
			_ptableVersion = version;
			_skipIndexVerify = skipIndexVerify;
		}

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var table = new HashListMemTable(_ptableVersion, maxSize: 50);
			table.Add(0x010100000000, 0x0001, 0x0001);
			table.Add(0x010500000000, 0x0001, 0x0002);
			table.Add(0x010200000000, 0x0001, 0x0003);
			table.Add(0x010200000000, 0x0002, 0x0004);
			table.Add(0x010300000000, 0x0001, 0xFFF1);
			table.Add(0x010300000000, 0x0003, 0xFFF3);
			table.Add(0x010300000000, 0x0005, 0xFFF5);
			_ptable = PTable.FromMemtable(table, Filename, cacheDepth: 0, skipIndexVerify: _skipIndexVerify);
		}

		public override void TestFixtureTearDown() {
			_ptable.Dispose();
			base.TestFixtureTearDown();
		}

		private ulong GetHash(ulong value) {
			return _ptableVersion == PTableVersions.IndexV1 ? value >> 32 : value;
		}

		[Test]
		public void range_query_of_non_existing_stream_returns_nothing() {
			var list = _ptable.GetRange(0x14, 0x01, 0x02).ToArray();
			Assert.AreEqual(0, list.Length);
		}

		[Test]
		public void range_query_of_non_existing_version_returns_nothing() {
			var list = _ptable.GetRange(0x010100000000, 0x03, 0x05).ToArray();
			Assert.AreEqual(0, list.Length);
		}

		[Test]
		public void range_query_with_hole_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x01, 0x05).ToArray();
			Assert.AreEqual(3, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x05, list[0].Version);
			Assert.AreEqual(0xfff5, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x03, list[1].Version);
			Assert.AreEqual(0xfff3, list[1].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[2].Stream);
			Assert.AreEqual(0x01, list[2].Version);
			Assert.AreEqual(0xfff1, list[2].Position);
		}

		[Test]
		public void query_with_start_in_range_but_not_end_results_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x01, 0x04).ToArray();
			Assert.AreEqual(2, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x03, list[0].Version);
			Assert.AreEqual(0xfff3, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x01, list[1].Version);
			Assert.AreEqual(0xfff1, list[1].Position);
		}

		[Test]
		public void query_with_end_in_range_but_not_start_results_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x00, 0x03).ToArray();
			Assert.AreEqual(2, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x03, list[0].Version);
			Assert.AreEqual(0xfff3, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x01, list[1].Version);
			Assert.AreEqual(0xfff1, list[1].Position);
		}

		[Test]
		public void query_with_end_and_start_exclusive_results_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x00, 0x06).ToArray();
			Assert.AreEqual(3, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x05, list[0].Version);
			Assert.AreEqual(0xfff5, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x03, list[1].Version);
			Assert.AreEqual(0xfff3, list[1].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[2].Stream);
			Assert.AreEqual(0x01, list[2].Version);
			Assert.AreEqual(0xfff1, list[2].Position);
		}

		[Test]
		public void query_with_end_inside_the_hole_in_list_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x00, 0x04).ToArray();
			Assert.AreEqual(2, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x03, list[0].Version);
			Assert.AreEqual(0xfff3, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x01, list[1].Version);
			Assert.AreEqual(0xfff1, list[1].Position);
		}

		[Test]
		public void query_with_start_inside_the_hole_in_list_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x02, 0x06).ToArray();
			Assert.AreEqual(2, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x05, list[0].Version);
			Assert.AreEqual(0xfff5, list[0].Position);
			Assert.AreEqual(GetHash(0x010300000000), list[1].Stream);
			Assert.AreEqual(0x03, list[1].Version);
			Assert.AreEqual(0xfff3, list[1].Position);
		}

		[Test]
		public void query_with_start_and_end_inside_the_hole_in_list_returns_items_included() {
			var list = _ptable.GetRange(0x010300000000, 0x02, 0x04).ToArray();
			Assert.AreEqual(1, list.Length);
			Assert.AreEqual(GetHash(0x010300000000), list[0].Stream);
			Assert.AreEqual(0x03, list[0].Version);
			Assert.AreEqual(0xfff3, list[0].Position);
		}

		[Test]
		public void query_with_start_and_end_less_than_all_items_returns_nothing() {
			var list = _ptable.GetRange(0x010300000000, 0x00, 0x00).ToArray();
			Assert.AreEqual(0, list.Length);
		}

		[Test]
		public void query_with_start_and_end_greater_than_all_items_returns_nothing() {
			var list = _ptable.GetRange(0x010300000000, 0x06, 0x06).ToArray();
			Assert.AreEqual(0, list.Length);
		}
	}
}
