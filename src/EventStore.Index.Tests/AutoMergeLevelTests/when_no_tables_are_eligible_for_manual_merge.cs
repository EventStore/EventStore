using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	public class when_no_tables_are_eligible_for_manual_merge : when_max_auto_merge_level_is_set {
		public override void Setup() {
			base.Setup();
			AddTables(8);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
			var (level, table) = _result.MergedMap.GetTableForManualMerge();
			Assert.NotNull(table);

			_result = _result.MergedMap.AddPTable(table, _result.MergedMap.PrepareCheckpoint,
				_result.MergedMap.CommitCheckpoint, UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion,
				level: level,
				skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());
		}

		[Test]
		public void should_not_return_table_for_merge() {
			Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
			AddTables(3); //adding 3 tables will cause an auto merge, but not enough to give us tables for manual merge
			var (level, table) = _result.MergedMap.GetTableForManualMerge();
			Assert.Null(table);

			Assert.AreEqual(3, _result.MergedMap.InOrder().Count());
		}
	}
}
