// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Projections.Core.Tests {
	[SetUpFixture]
	public class TestsInitFixture {
		private readonly EventStore.Core.Tests.TestsInitFixture _initFixture =
			new EventStore.Core.Tests.TestsInitFixture();

		[OneTimeSetUp]
		public void SetUp() {
			_initFixture.SetUp();
		}

		[OneTimeTearDown]
		public void TearDown() {
			_initFixture.TearDown();
		}
	}
}
