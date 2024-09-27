// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Core.LogV3;
using Xunit;
using StreamId = System.UInt32;

namespace EventStore.Core.XUnit.Tests.LogV3 {
	public class LogV3StreamExistenceFilterInitializerTests {
		[Fact]
		public void can_initialize_empty() {
			var sut = new LogV3StreamExistenceFilterInitializer(new MockNameLookup(new()));

			var filter = new MockExistenceFilter();
			filter.Initialize(sut, 0);

			Assert.Equal(-1, filter.CurrentCheckpoint);
			Assert.Empty(filter.Streams);
		}

		[Fact]
		public void can_initialize_non_empty() {
			var sut = new LogV3StreamExistenceFilterInitializer(new MockNameLookup(
				new Dictionary<StreamId, string> {
					{ 1024, "1024" },
					{ 1026, "1026" },
			}));

			var filter = new MockExistenceFilter();
			filter.Initialize(sut, 0);

			Assert.Equal(1026, filter.CurrentCheckpoint);
			Assert.True(filter.MightContain("1024"));
			Assert.True(filter.MightContain("1026"));
			Assert.False(filter.MightContain("1028"));
		}
	}
}
