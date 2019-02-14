using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.IO;

namespace EventStore.Core.Tests.Integration {
	public class specification_with_a_single_node : SpecificationWithDirectoryPerTestFixture {
		protected MiniNode _node;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName, dbPath: Path.Combine(PathName, "db"), inMemDb: false);

			BeforeNodeStarts();

			_node.Start();

			Given();
		}

		protected virtual void BeforeNodeStarts() {
		}

		protected virtual void Given() {
		}

		protected void ShutdownNode() {
			_node.Shutdown(keepDb: true, keepPorts: true);
			_node = null;
		}

		protected void StartNode() {
			if (_node == null)
				_node = new MiniNode(PathName, dbPath: Path.Combine(PathName, "db"), inMemDb: false);

			BeforeNodeStarts();

			_node.Start();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_node.Shutdown();
			_node = null;
			base.TestFixtureTearDown();
		}
	}
}
