using System;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	public class when_max_auto_merge_level_is_reduced : when_max_auto_merge_level_is_set {
		public when_max_auto_merge_level_is_reduced() : base(5) {
		}

		[Test]
		public void should_merge_levels_above_max_level() {
			AddTables(201); //gives 1 level 0, 1 level 3 and 6 level 5s
			Assert.AreEqual(8, _result.MergedMap.InOrder().Count());
			var filename = GetFilePathFor("changemaxlevel");
			_result.MergedMap.SaveToFile(filename);
			_result.MergedMap.Dispose(TimeSpan.FromMilliseconds(100));
			_map.Dispose(TimeSpan.FromMilliseconds(100));
			_map = IndexMapTestFactory.FromFile(filename, maxAutoMergeLevel: 3);
			var (level, table) = _map.GetTableForManualMerge();
			Assert.Greater(level, _maxAutoMergeLevel);
			_result = _map.AddPTable(table, _result.MergedMap.PrepareCheckpoint, _result.MergedMap.CommitCheckpoint,
				UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion,
				level: level,
				skipIndexVerify: _skipIndexVerify);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
		}
	}
}
