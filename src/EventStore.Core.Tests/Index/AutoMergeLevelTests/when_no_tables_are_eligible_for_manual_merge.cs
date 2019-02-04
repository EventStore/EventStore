using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;
using static EventStore.Core.Index.TableIndex;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests
{
	public class when_no_tables_are_eligible_for_manual_merge: when_max_auto_merge_level_is_set
	{
		[SetUp]
		public override void Setup()
		{
            base.Setup();
			AddTables(8);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
			var manualMergeItem = TableItem.GetManualMergeTableItem();
			
			_result = _result.MergedMap.AddPTable((PTable)manualMergeItem.Table, manualMergeItem.PrepareCheckpoint, manualMergeItem.CommitCheckpoint, UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion, 
				level: manualMergeItem.Level,
				skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x=>x.MarkForDestruction());
		}

		[Test]
		public void should_not_return_table_for_merge()
		{
			Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
			AddTables(3); //adding 3 tables will cause an auto merge, but not enough to give us tables for manual merge
			Assert.AreEqual(3,_result.MergedMap.InOrder().Count());

			var ptableLevels = _result.MergedMap.GetAllPTablesWithLevels();
			Assert.AreEqual(ptableLevels[_maxAutoMergeLevel].Count, 1);
			Assert.AreEqual(ptableLevels[_maxAutoMergeLevel+1], null);
		}
	}
}