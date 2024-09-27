// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Utils {
	public class NumberExtensionsTests {
		[Fact]
		public void CanRoundUp() {
			Assert.Equal(0, 0L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 1L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 2L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 3L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 4L.RoundUpToMultipleOf(4));
			Assert.Equal(8, 5L.RoundUpToMultipleOf(4));
		}

		[Fact]
		public void CanRoundDown() {
			Assert.Equal(0, 3L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 4L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 5L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 6L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 7L.RoundDownToMultipleOf(4));
			Assert.Equal(8, 8L.RoundDownToMultipleOf(4));
		}
	}
}
