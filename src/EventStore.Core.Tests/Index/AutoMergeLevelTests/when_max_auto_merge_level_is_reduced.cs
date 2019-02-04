using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;
using static EventStore.Core.Index.TableIndex;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests
{
	public class when_max_auto_merge_level_is_reduced : when_max_auto_merge_level_is_set
	{
		public when_max_auto_merge_level_is_reduced():base(5)
		{
		}

		[Test]
		public void should_merge_levels_above_max_level()
		{
			AddTables(201); //gives 1 level 0, 1 level 3 and 6 level 5s
			Assert.AreEqual(8,_result.MergedMap.InOrder().Count());
			var ptableLevels = _result.MergedMap.GetAllPTablesWithLevels();

			var filename = GetFilePathFor("changemaxlevel");
			_result.MergedMap.SaveToFile(filename);
			_result.MergedMap.Dispose(TimeSpan.FromMilliseconds(100));
			_map.Dispose(TimeSpan.FromMilliseconds(100));
			_map = IndexMapTestFactory.FromFile(filename, maxAutoMergeLevel:3);
			ptableLevels = _map.GetAllPTablesWithLevels();
			Assert.AreEqual(ptableLevels[0].Count, 1);
			Assert.AreEqual(ptableLevels[3].Count, 1);
			Assert.AreEqual(ptableLevels[5].Count, 6);

			var manualMergeItem = TableItem.GetManualMergeTableItem();
			_result = _map.AddPTable((PTable)manualMergeItem.Table, manualMergeItem.PrepareCheckpoint, manualMergeItem.CommitCheckpoint, UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion, 
				level: manualMergeItem.Level,
				skipIndexVerify: _skipIndexVerify);
			Assert.AreEqual(2,_result.MergedMap.InOrder().Count());
		}
	}
}