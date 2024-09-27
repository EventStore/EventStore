// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Enumerators;

public partial class EnumeratorTests {
	[TestFixture]
	public class TestFixtureWithMiniNodeConnection : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode<LogFormat.V2, string> Node { get; private set; }
		protected IEventStoreConnection NodeConnection { get; private set; }

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			Node = new MiniNode<LogFormat.V2, string>(PathName);
			await Node.Start();
			NodeConnection = TestConnection.To(Node, TcpType.Ssl, new UserCredentials("admin", "changeit"));
			await NodeConnection.ConnectAsync();
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			NodeConnection?.Dispose();
			await Node.Shutdown();
			await base.TestFixtureTearDown();
		}
	}
}
