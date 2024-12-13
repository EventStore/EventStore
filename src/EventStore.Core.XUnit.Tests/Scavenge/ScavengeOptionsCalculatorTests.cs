// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messaging;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Scavenging;
using Microsoft.Extensions.Configuration;
using Xunit;
using EventStore.Core.Services.UserManagement;
using System;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class ScavengeOptionsCalculatorTests {
	private static ScavengeOptionsCalculator GenSut(
		KeyValuePair<string, string?>[]? vNodeOptions = null,
		int? threshold = null) {

		vNodeOptions ??= [];
		var message = new ClientMessage.ScavengeDatabase(
			envelope: IEnvelope.NoOp,
			correlationId: Guid.NewGuid(),
			user: SystemAccounts.Anonymous,
			startFromChunk: 0,
			threads: 1,
			threshold: threshold,
			throttlePercent: 100,
			syncOnly: false);

		var config = new ConfigurationBuilder().AddInMemoryCollection(
				vNodeOptions.Append(new("EventStore:ClusterSize", "1")))
			.Build();
		var options = ClusterVNodeOptions.FromConfiguration(config);
		var sut = new ScavengeOptionsCalculator(options, message);
		return sut;
	}

	[Fact]
	public void merging_enabled_by_default() {
		var sut = GenSut([
		]);

		Assert.True(sut.MergeChunks);
	}

	[Fact]
	public void merging_can_be_disabled() {
		var sut = GenSut([
			new("EventStore:DisableScavengeMerging", "true"),
		]);

		Assert.False(sut.MergeChunks);
	}

	[Fact]
	public void merging_is_disabled_when_archiving_is_enabled() {
		var sut = GenSut(
			vNodeOptions: [
				new("EventStore:Archive:StorageType", "S3"),
			]);

		Assert.False(sut.MergeChunks);
	}

	[Theory]
	[InlineData(null, 0)]
	[InlineData(-5, -5)]
	[InlineData(0, 0)]
	[InlineData(5, 5)]
	public void threshold_can_be_set(int? configuredThreshold, int expectedThreshold) {
		var sut = GenSut(threshold: configuredThreshold);

		Assert.Equal(expectedThreshold, sut.ChunkExecutionThreshold);
	}
}
