// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService {
	[TestFixture]
	public class when_creating {
		[Test]
		public void it_can_ce_created() {
			var it = new Core.Services.AwakeReaderService.AwakeService();
			Assert.NotNull(it);
		}
	}
}
