using System;
using System.Diagnostics;
using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Http {
	class TestSuiteMarkerBase {
		public static MiniNode _node;
		public static IEventStoreConnection _connection;
		public static int _counter;
		private SpecificationWithDirectoryPerTestFixture _directory;

		[OneTimeSetUp]
		public void SetUp() {
			WebRequest.DefaultWebProxy = new WebProxy();
			_counter = 0;
			_directory = new SpecificationWithDirectoryPerTestFixture();
			_directory.TestFixtureSetUp();
			_node = new MiniNode(_directory.PathName, skipInitializeStandardUsersCheck: false, enableTrustedAuth: true);
			_node.Start();

			_connection = TestConnection.Create(_node.TcpEndPoint);
			_connection.ConnectAsync().Wait();
		}

		[OneTimeTearDown]
		public void TearDown() {
			_connection.Close();
			_node.Shutdown();
			_connection = null;
			_node = null;
			_directory.TestFixtureTearDown();
		}
	}
}
