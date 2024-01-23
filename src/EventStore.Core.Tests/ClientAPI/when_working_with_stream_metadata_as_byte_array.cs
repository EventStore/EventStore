extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using GrpcClientStreams::EventStore.Client;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamDeletedException = GrpcClient::EventStore.Client.StreamDeletedException;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using WrongExpectedVersionException = GrpcClient::EventStore.Client.WrongExpectedVersionException;

namespace EventStore.Core.Tests.ClientAPI {

	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_working_with_stream_metadata_as_byte_array<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		private MiniNode<TLogFormat, TStreamId> _node;
		private IEventStoreClient _connection;

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();
			_node = new MiniNode<TLogFormat, TStreamId>(PathName);
			await _node.Start();

			_connection = BuildConnection(_node);
			await _connection.ConnectAsync();
		}

		protected virtual IEventStoreClient BuildConnection(MiniNode<TLogFormat, TStreamId> node) {
			return new GrpcEventStoreConnection(node.HttpEndPoint);
		}

		[OneTimeTearDown]
		public override async Task TestFixtureTearDown() {
			await _connection.Close();
			await _node.Shutdown();
			await base.TestFixtureTearDown();
		}

		[Test]
		public async Task setting_empty_metadata_works() {
			const string stream = "setting_empty_metadata_works";

			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, new StreamMetadata());

			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(new byte[0], meta.Metadata.ToJsonBytes());
		}

		[Test]
		public async Task setting_metadata_few_times_returns_last_metadata() {
			const string stream = "setting_metadata_few_times_returns_last_metadata";

			var metadata = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);
			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadata, meta.Metadata);

			metadata = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, 0, metadata);
			meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadata, meta.Metadata);
		}

		[Test]
		public async Task trying_to_set_metadata_with_wrong_expected_version_fails() {
			const string stream = "trying_to_set_metadata_with_wrong_expected_version_fails";
			await AssertEx.ThrowsAsync<WrongExpectedVersionException>(() => _connection.SetStreamMetadataAsync(stream, 5, RandomStreamMetadata()));
		}

		[Test]
		public async Task setting_metadata_with_expected_version_any_works() {
			const string stream = "setting_metadata_with_expected_version_any_works";

			var metadataBytes = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.Any, metadataBytes);
			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadataBytes, meta.Metadata);

			metadataBytes = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.Any, metadataBytes);
			meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadataBytes, meta.Metadata);
		}

		[Test]
		public async Task setting_metadata_for_not_existing_stream_works() {
			const string stream = "setting_metadata_for_not_existing_stream_works";
			var metadataBytes = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadataBytes);

			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadataBytes, meta.Metadata);
		}

		[Test]
		public async Task setting_metadata_for_existing_stream_works() {
			const string stream = "setting_metadata_for_existing_stream_works";

			await _connection.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent(),
				TestEvent.NewTestEvent());

			var metadataBytes = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadataBytes);

			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(metadataBytes, meta.Metadata);
		}

		[Test]
		public async Task setting_metadata_for_deleted_stream_throws_stream_deleted_exception() {
			const string stream = "setting_metadata_for_deleted_stream_throws_stream_deleted_exception";

			await _connection.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: true);

			var metadataBytes = Guid.NewGuid().ToByteArray();
			await AssertEx.ThrowsAsync<StreamDeletedException>(
				() => _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, RandomStreamMetadata()));
		}

		[Test]
		public async Task getting_metadata_for_nonexisting_stream_returns_empty_byte_array() {
			const string stream = "getting_metadata_for_nonexisting_stream_returns_empty_byte_array";

			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(-1, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(new byte[0], meta.Metadata.ToJsonBytes());
		}

		[Test]
		public async Task getting_metadata_for_deleted_stream_returns_empty_byte_array_and_signals_stream_deletion() {
			const string stream =
				"getting_metadata_for_deleted_stream_returns_empty_byte_array_and_signals_stream_deletion";

			var metadataBytes = RandomStreamMetadata();
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadataBytes);

			await _connection.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: true);

			var meta = await _connection.GetStreamMetadataAsRawBytesAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(true, meta.StreamDeleted);
			Assert.AreEqual(EventNumber.DeletedStream, meta.MetastreamRevision!.Value.ToInt64());
			Assert.AreEqual(new byte[0], meta.Metadata.ToJsonBytes());
		}

		private StreamMetadata RandomStreamMetadata() {
			return new StreamMetadata(customMetadata: JsonDocument.Parse(new JsonObject {
				["foo"] = Guid.NewGuid().ToString()
			}.ToJson()));
		}
	}
}
