using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests
{
	public class when_no_tables_have_yet_reached_maximum_automerge_level: when_max_auto_merge_level_is_set
	{
		[Test]
		public void should_not_return_table_for_merge()
		{
			AddTables(3);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
			var ptableLevels = _result.MergedMap.GetAllPTablesWithLevels();
			Assert.AreEqual(ptableLevels[0].Count, 1);
			Assert.AreEqual(ptableLevels[1].Count, 1);
			Assert.AreEqual(ptableLevels[_maxAutoMergeLevel], null);
			Assert.AreEqual(ptableLevels[_maxAutoMergeLevel+1], null);
		}
	}
}