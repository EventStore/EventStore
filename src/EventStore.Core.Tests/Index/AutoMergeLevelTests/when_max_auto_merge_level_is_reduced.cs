// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
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
			_result = _map.TryManualMerge(
				_fileNameProvider,
				_ptableVersion,
				skipIndexVerify: _skipIndexVerify);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
		}
	}
}
