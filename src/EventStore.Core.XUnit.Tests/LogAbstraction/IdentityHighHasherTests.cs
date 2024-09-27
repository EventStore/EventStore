// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using Xunit;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	public class IdentityHighHasherTests {
		[Theory]
		[InlineData(0, 0)]
		[InlineData(5, 0)]
		[InlineData(0xAAAA_BBBB, 0)]
		[InlineData(0xFFFF_FFFF, 0)]
		public void hashes_correctly(LogV3StreamId x, uint expected) {
			var sut = new IdentityHighHasher();
			Assert.Equal(expected, sut.Hash(x));
		}
	}
}
