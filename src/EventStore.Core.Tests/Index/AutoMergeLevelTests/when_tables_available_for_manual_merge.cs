using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;
using static EventStore.Core.Index.TableIndex;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests
{
	public class when_tables_available_for_manual_merge : when_max_auto_merge_level_is_set
	{
		[Test]
		public void should_merge_pending_tables_at_max_auto_merge_level()
		{
			AddTables(100);
			Assert.AreEqual(25, _result.MergedMap.InOrder().Count());
			var ptableLevels = _result.MergedMap.GetAllPTablesWithLevels();
			Assert.AreEqual(ptableLevels[_maxAutoMergeLevel].Count, 25);

			var manualMergeItem = TableItem.GetManualMergeTableItem();
			_result = _result.MergedMap.AddPTable((PTable)manualMergeItem.Table, manualMergeItem.PrepareCheckpoint, manualMergeItem.CommitCheckpoint, UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion, 
				level: manualMergeItem.Level,
				skipIndexVerify: _skipIndexVerify);
			Assert.AreEqual(1,_result.MergedMap.InOrder().Count());
			
		}
	}
}