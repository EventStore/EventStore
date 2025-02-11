// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using EventStore.Common.Exceptions;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

// Some other tests are in ClusterNodeOptionsTests/when_building
public class ClusterVNodeOptionsValidatorTests {
	[Theory]
	[InlineData(false, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, false, false)]
	[InlineData(true, true, true)]
	public void archiver_requires_read_only_replica(bool archiver, bool readOnlyReplica, bool expectedValid) {
		// given
		var options = new ClusterVNodeOptions {
			Cluster = new() {
				Archiver = archiver,
				ClusterSize = 3,
				ReadOnlyReplica = readOnlyReplica,
			}
		};

		// when
		void When() {
			ClusterVNodeOptionsValidator.Validate(options);
		}

		// then
		if (expectedValid) {
			When();
		} else {
			Assert.Throws<InvalidConfigurationException>(When);
		}
	}
}
