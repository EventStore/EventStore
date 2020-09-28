using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	public class when_tables_available_for_manual_merge : when_max_auto_merge_level_is_set {
		[Test]
		public void should_merge_pending_tables_at_max_auto_merge_level() {
			AddTables(100);
			Assert.AreEqual(25, _result.MergedMap.InOrder().Count());
			_result = _result.MergedMap.TryManualMerge(UpgradeHash, ExistsAt,
				RecordExistsAt, _fileNameProvider, _ptableVersion,
				skipIndexVerify: _skipIndexVerify);
			Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
		}
	}
}
