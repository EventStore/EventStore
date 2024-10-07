// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests;

public class when_no_tables_have_yet_reached_maximum_automerge_level : when_max_auto_merge_level_is_set {
	[Test]
	public void should_not_manually_merge_any_table() {
		AddTables(3);
		Assert.AreEqual(2, _result.MergedMap.InOrder().Count());
		var result = _result.MergedMap.TryManualMerge(
			_fileNameProvider,
			_ptableVersion,
			skipIndexVerify: _skipIndexVerify);
		Assert.False(result.HasMergedAny);
	}
}
