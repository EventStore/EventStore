using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class when_working_with_metadata : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;
		private IEventStoreConnection _connection;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			_node.Start();

			_connection = BuildConnection(_node);
			_connection.ConnectAsync().Wait();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_connection.Close();
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		[Test]
		public void when_getting_metadata_for_an_existing_stream_and_no_metadata_exists() {
			const string stream = "when_getting_metadata_for_an_existing_stream_and_no_metadata_exists";

			_connection.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()).Wait();

			var meta = _connection.GetStreamMetadataAsRawBytesAsync(stream).Result;
			Assert.AreEqual(stream, meta.Stream);
			Assert.AreEqual(false, meta.IsStreamDeleted);
			Assert.AreEqual(-1, meta.MetastreamVersion);
			Assert.AreEqual(Helper.UTF8NoBom.GetBytes(""), meta.StreamMetadata);
		}
	}
}
