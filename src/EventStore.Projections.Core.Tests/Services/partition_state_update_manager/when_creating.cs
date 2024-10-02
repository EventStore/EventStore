// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services.Processing.Partitioning;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.partition_state_update_manager {
	[TestFixture]
	public class when_creating {
		[Test]
		public void no_exceptions_are_thrown() {
			new PartitionStateUpdateManager(ProjectionNamesBuilder.CreateForTest("projection"));
		}

		[Test]
		public void null_naming_builder_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => { new PartitionStateUpdateManager(null); });
		}
	}
}
