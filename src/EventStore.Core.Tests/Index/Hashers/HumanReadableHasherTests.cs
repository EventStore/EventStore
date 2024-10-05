// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Hashers;

public class HumanReadableHasherTests {
	[Test]
	public void hashes_original_stream() {
		var sut = new HumanReadableHasher();
		Assert.AreEqual('a', sut.Hash("ma-1"));
	}

	[Test]
	public void hashes_meta_stream() {
		var sut = new HumanReadableHasher();
		Assert.AreEqual('m', sut.Hash("$$ma-1"));
	}

	[Test]
	public void hashes_empty_string() {
		var sut = new HumanReadableHasher();
		Assert.AreEqual(0UL, sut.Hash(""));
	}
}
