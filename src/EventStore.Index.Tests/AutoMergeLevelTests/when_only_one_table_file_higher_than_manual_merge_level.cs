using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests {
	[TestFixture]
	public class when_only_one_table_file_higher_than_manual_merge_level : when_max_auto_merge_level_is_set {
		public override void Setup() {
			base.Setup();
			AddTables(5);
			_map.Dispose(TimeSpan.FromMilliseconds(100));
			var filename = GetFilePathFor("indexmap");
			_result.MergedMap.SaveToFile(filename);
			_result.MergedMap.Dispose(TimeSpan.FromMilliseconds(5));
			_map = IndexMapTestFactory.FromFile(filename, maxAutoMergeLevel: 1);
		}

		[Test]
		public void no_table_should_be_available_for_merge() {
			var (_, table) = _map.GetTableForManualMerge();
			Assert.Null(table);
		}
	}
}
