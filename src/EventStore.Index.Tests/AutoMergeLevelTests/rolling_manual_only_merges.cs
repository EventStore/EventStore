using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	[TestFixture]
	public class rolling_manual_only_merges : when_max_auto_merge_level_is_set {
		public rolling_manual_only_merges() : base(0) {
		}

		[Test]
		public void alternating_table_dumps_and_manual_merges_should_merge_correctly() {
			AddTables(1);
			var (level, table) = _result.MergedMap.GetTableForManualMerge();
			Assert.Null(table); //if there is only one table it shouldn't be merged
			Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
			for (int i = 0; i < 100; i++) {
				AddTables(1);
				Assert.AreEqual(2, _result.MergedMap.InOrder().Count());

				(level, table) = _result.MergedMap.GetTableForManualMerge();
				_result = _result.MergedMap.AddPTable(table, _result.MergedMap.PrepareCheckpoint,
					_result.MergedMap.CommitCheckpoint, UpgradeHash, ExistsAt, RecordExistsAt, _fileNameProvider,
					_ptableVersion, level, 16, false);
				_result.ToDelete.ForEach(x => x.MarkForDestruction());
				Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
			}
		}
	}
}
