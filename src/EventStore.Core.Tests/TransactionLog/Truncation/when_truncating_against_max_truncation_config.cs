// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Validation;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Transforms.Identity;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation;

[TestFixture]
public class when_truncating_against_max_truncation_config : SpecificationWithDirectoryPerTestFixture {
	private TFChunkDbConfig _config;

	[OneTimeSetUp]
	public override async Task TestFixtureSetUp() {
		await base.TestFixtureSetUp();

		// writer checkpoint = 5500, truncate to 0, max truncation = 1000
		_config = TFChunkHelper.CreateDbConfigEx(PathName, 5500, 5500, 5500, -1, 0, 1000, maxTruncation: 1000);

		DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000001"));
		DbUtil.CreateMultiChunk(_config, 0, 2, GetFilePathFor("chunk-000000.000002"));
		DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000001"));
		DbUtil.CreateMultiChunk(_config, 3, 10, GetFilePathFor("chunk-000003.000002"));
		DbUtil.CreateMultiChunk(_config, 7, 8, GetFilePathFor("chunk-000007.000001"));
		DbUtil.CreateOngoingChunk(_config, 11, GetFilePathFor("chunk-000011.000000"));
	}

	[OneTimeTearDown]
	public override Task TestFixtureTearDown() {
		return base.TestFixtureTearDown();
	}

	[Test]
	public void truncate_above_max_throws_exception() {
		Assert.ThrowsAsync<Exception>(async () => {
			var truncator = new TFChunkDbTruncator(_config, _ => new IdentityChunkTransformFactory());
			await truncator.TruncateDb(0, CancellationToken.None);
		});
	}

	[Test]
	public void truncate_within_max_does_not_throw_exception() {

		Assert.DoesNotThrowAsync(async () => {
			var truncator = new TFChunkDbTruncator(_config, _ => new IdentityChunkTransformFactory());
			await truncator.TruncateDb(4800 ,CancellationToken.None);
		});
	}
}
