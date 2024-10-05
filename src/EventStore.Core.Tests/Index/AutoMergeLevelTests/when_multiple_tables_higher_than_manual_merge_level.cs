// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests;

[TestFixture]
public class when_multiple_tables_higher_than_manual_merge_level : when_max_auto_merge_level_is_set {
	public override void Setup() {
		base.Setup();
		AddTables(8);
		_map.Dispose(TimeSpan.FromMilliseconds(100));
		var filename = GetFilePathFor("indexmap");
		_result.MergedMap.SaveToFile(filename);
		_result.MergedMap.Dispose(TimeSpan.FromMilliseconds(100));
		_map = IndexMapTestFactory.FromFile(filename, maxAutoMergeLevel: 1);
	}

	[Test]
	public void tables_should_be_merged() {
		_result = _map.TryManualMerge(
			_fileNameProvider,
			_ptableVersion,
			skipIndexVerify: _skipIndexVerify);
		Assert.AreEqual(1, _result.MergedMap.InOrder().Count());
	}
}
