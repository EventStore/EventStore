// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class when_creating_chunked_transaction_file_writer {
	[Test]
	public void a_null_config_throws_argument_null_exception() {
		Assert.Throws<ArgumentNullException>(() => new TFChunkWriter(null));
	}
}
