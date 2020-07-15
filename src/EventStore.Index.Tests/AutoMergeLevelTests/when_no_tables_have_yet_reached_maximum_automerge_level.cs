using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	public class when_no_tables_have_yet_reached_maximum_automerge_level : when_max_auto_merge_level_is_set {
		[Test]
		public void should_not_return_table_for_merge() {
			AddTables(3);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
			var (level, table) = _result.MergedMap.GetTableForManualMerge();
			Assert.AreEqual(1, level);
			Assert.Null(table);
		}
	}
}
