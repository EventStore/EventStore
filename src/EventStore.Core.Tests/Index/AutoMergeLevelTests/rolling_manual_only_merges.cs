// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests;

[TestFixture]
public class rolling_manual_only_merges : when_max_auto_merge_level_is_set {
	public rolling_manual_only_merges() : base(0) {
	}

	[Test]
	public void alternating_table_dumps_and_manual_merges_should_merge_correctly() {
		AddTables(1);
		Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
		for (int i = 0; i < 100; i++) {
			AddTables(1);
			Assert.AreEqual(2, _result.MergedMap.InOrder().Count());

			_result = _result.MergedMap.TryManualMerge(_fileNameProvider,
				_ptableVersion, 16, false);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());
			Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
		}
	}
}
