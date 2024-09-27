// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using Xunit;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	public class IdentityLowHasherTests {
		[Theory]
		[InlineData(0, 0)]
		[InlineData(5, 5)]
		[InlineData(0xAAAA_BBBB, 0xAAAA_BBBB)]
		[InlineData(0xFFFF_FFFF, 0xFFFF_FFFF)]
		public void hashes_correctly(LogV3StreamId x, uint expected) {
			var sut = new IdentityLowHasher();
			Assert.Equal(expected, sut.Hash(x));
		}
	}
}
