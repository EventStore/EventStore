// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogV3;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogV3 {
	public class LogV3SizerTests {
		[Fact]
		public void can_get_size() {
			var sut = new LogV3Sizer();
			Assert.Equal(4, sut.GetSizeInBytes(12345));
		}
	}
}
