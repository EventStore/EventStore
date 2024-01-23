extern alias GrpcClientStreams;
extern alias GrpcClient;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.Core.Tests.ClientAPI.Helpers.ExpectedVersion;
using WrongExpectedVersionException = EventStore.Core.Tests.ClientAPI.Helpers.WrongExpectedVersionException;
using StreamAcl = GrpcClientStreams::EventStore.Client.StreamAcl;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_working_with_stream_metadata_as_structured_info<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
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
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(Helper.UTF8NoBom.GetBytes("{}"), meta.Metadata.ToJson());
		}

		[Test]
		public async Task setting_metadata_few_times_returns_last_metadata_info() {
			const string stream = "setting_metadata_few_times_returns_last_metadata_info";
			var metadata =
				new StreamMetadata(17, TimeSpan.FromSeconds(0xDEADBEEF), 10, TimeSpan.FromSeconds(0xABACABA));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);

			metadata = new StreamMetadata(37, TimeSpan.FromSeconds(0xBEEFDEAD), 24,
				TimeSpan.FromSeconds(0xDABACABAD));
			await _connection.SetStreamMetadataAsync(stream, 0, metadata);

			meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(1, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);
		}

		[Test]
		public async Task trying_to_set_metadata_with_wrong_expected_version_fails() {
			const string stream = "trying_to_set_metadata_with_wrong_expected_version_fails";
			await AssertEx.ThrowsAsync<WrongExpectedVersionException>(() =>
				_connection.SetStreamMetadataAsync(stream, 2, new StreamMetadata()));
		}

		[Test]
		public async Task setting_metadata_with_expected_version_any_works() {
			const string stream = "setting_metadata_with_expected_version_any_works";
			var metadata =
				new StreamMetadata(17, TimeSpan.FromSeconds(0xDEADBEEF), 10, TimeSpan.FromSeconds(0xABACABA));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.Any, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);

			metadata = new StreamMetadata(37, TimeSpan.FromSeconds(0xBEEFDEAD), 24,
				TimeSpan.FromSeconds(0xDABACABAD));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.Any, metadata);

			meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(1, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);
		}

		[Test]
		public async Task setting_metadata_for_not_existing_stream_works() {
			const string stream = "setting_metadata_for_not_existing_stream_works";
			var metadata =
				new StreamMetadata(17, TimeSpan.FromSeconds(0xDEADBEEF), 10, TimeSpan.FromSeconds(0xABACABA));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);
		}

		[Test]
		public async Task setting_metadata_for_existing_stream_works() {
			const string stream = "setting_metadata_for_existing_stream_works";

			await _connection.AppendToStreamAsync(stream, ExpectedVersion.NoStream, TestEvent.NewTestEvent());

			var metadata =
				new StreamMetadata(17, TimeSpan.FromSeconds(0xDEADBEEF), 10, TimeSpan.FromSeconds(0xABACABA));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(metadata.MaxCount, meta.Metadata.MaxCount);
			Assert.AreEqual(metadata.MaxAge, meta.Metadata.MaxAge);
			Assert.AreEqual(metadata.TruncateBefore, meta.Metadata.TruncateBefore);
			Assert.AreEqual(metadata.CacheControl, meta.Metadata.CacheControl);
		}

		[Test]
		public async Task getting_metadata_for_nonexisting_stream_returns_empty_stream_metadata() {
			const string stream = "getting_metadata_for_nonexisting_stream_returns_empty_stream_metadata";

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(-1, meta.MetastreamRevision);
			Assert.AreEqual(null, meta.Metadata.MaxCount);
			Assert.AreEqual(null, meta.Metadata.MaxAge);
			Assert.AreEqual(null, meta.Metadata.TruncateBefore);
			Assert.AreEqual(null, meta.Metadata.CacheControl);
		}

		[Test, Ignore("You can't get stream metadata for metastream through ClientAPI")]
		public async Task getting_metadata_for_metastream_returns_correct_metadata() {
			const string stream = "$$getting_metadata_for_metastream_returns_correct_metadata";

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(-1, meta.MetastreamRevision);
			Assert.AreEqual(1, meta.Metadata.MaxCount);
			Assert.AreEqual(null, meta.Metadata.MaxAge);
			Assert.AreEqual(null, meta.Metadata.TruncateBefore);
			Assert.AreEqual(null, meta.Metadata.CacheControl);
		}

		[Test]
		public async Task getting_metadata_for_deleted_stream_returns_empty_stream_metadata_and_signals_stream_deletion() {
			const string stream =
				"getting_metadata_for_deleted_stream_returns_empty_stream_metadata_and_signals_stream_deletion";

			var metadata =
				new StreamMetadata(17, TimeSpan.FromSeconds(0xDEADBEEF), 10, TimeSpan.FromSeconds(0xABACABA));
			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			await _connection.DeleteStreamAsync(stream, ExpectedVersion.NoStream, hardDelete: true);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(true, meta.StreamDeleted);
			Assert.AreEqual(EventNumber.DeletedStream, meta.MetastreamRevision);
			Assert.AreEqual(null, meta.Metadata.MaxCount);
			Assert.AreEqual(null, meta.Metadata.MaxAge);
			Assert.AreEqual(null, meta.Metadata.TruncateBefore);
			Assert.AreEqual(null, meta.Metadata.CacheControl);
			Assert.AreEqual(null, meta.Metadata.Acl);
		}

		// gRPC client doesn't expose a way to provide a JSON representation of a stream metadata.
		// [Test]
		// public async Task setting_correctly_formatted_metadata_as_raw_allows_to_read_it_as_structured_metadata() {
		// 	const string stream =
		// 		"setting_correctly_formatted_metadata_as_raw_allows_to_read_it_as_structured_metadata";
  //
		// 	var rawMeta = Helper.UTF8NoBom.GetBytes(@"{
  //                                                          ""$maxCount"": 17,
  //                                                          ""$maxAge"": 123321,
  //                                                          ""$tb"": 23,
  //                                                          ""$cacheControl"": 7654321,
  //                                                          ""$acl"": {
  //                                                              ""$r"": ""readRole"",
  //                                                              ""$w"": ""writeRole"",
  //                                                              ""$d"": ""deleteRole"",
  //                                                              ""$mw"": ""metaWriteRole""
  //                                                          },
  //                                                          ""customString"": ""a string"",
  //                                                          ""customInt"": -179,
  //                                                          ""customDouble"": 1.7,
  //                                                          ""customLong"": 123123123123123123,
  //                                                          ""customBool"": true,
  //                                                          ""customNullable"": null,
  //                                                          ""customRawJson"": {
  //                                                              ""subProperty"": 999
  //                                                          }
  //                                                     }");
  //
		// 	await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, rawMeta);
  //
		// 	var meta = await _connection.GetStreamMetadataAsync(stream);
		// 	Assert.AreEqual(stream, meta.StreamName);
		// 	Assert.AreEqual(false, meta.StreamDeleted);
		// 	Assert.AreEqual(0, meta.MetastreamRevision);
		// 	Assert.AreEqual(17, meta.Metadata.MaxCount);
		// 	Assert.AreEqual(TimeSpan.FromSeconds(123321), meta.Metadata.MaxAge);
		// 	Assert.AreEqual(23, meta.Metadata.TruncateBefore);
		// 	Assert.AreEqual(TimeSpan.FromSeconds(7654321), meta.Metadata.CacheControl);
  //
		// 	Assert.NotNull(meta.Metadata.Acl);
		// 	Assert.AreEqual("readRole", meta.Metadata.Acl.ReadRoles[0]);
		// 	Assert.AreEqual("writeRole", meta.Metadata.Acl.WriteRoles[0]);
		// 	Assert.AreEqual("deleteRole", meta.Metadata.Acl.DeleteRoles[0]);
		// 	// meta role removed to allow reading
		// 	//            Assert.AreEqual("metaReadRole", meta.StreamMetadata.Acl.MetaReadRole);
		// 	Assert.AreEqual("metaWriteRole", meta.Metadata.Acl.MetaWriteRoles[0]);
  //
		// 	Assert.AreEqual("a string", meta.Metadata.CustomMetadata!.RootElement.GetProperty("customString").GetString());
		// 	Assert.AreEqual(-179, meta.Metadata.CustomMetadata!.RootElement.GetProperty("customInt").GetInt32());
		// 	Assert.AreEqual(1.7, meta.Metadata.CustomMetadata!.RootElement.GetProperty("customDouble").GetDouble());
		// 	Assert.AreEqual(123123123123123123L, meta.Metadata.CustomMetadata!.RootElement.GetProperty("customLong").GetInt64());
		// 	Assert.AreEqual(true, meta.Metadata.CustomMetadata.RootElement.GetProperty("customBool").GetBoolean());
		// 	Assert.IsFalse(meta.Metadata.CustomMetadata.RootElement.TryGetProperty("customNullable", out var ignore));
		// 	Assert.AreEqual(@"{""subProperty"":999}", meta.Metadata.CustomMetadata.RootElement.GetProperty("customRawJson").ToJson());
		// }

		[Test]
		public async Task setting_structured_metadata_with_custom_properties_returns_them_untouched() {
			const string stream = "setting_structured_metadata_with_custom_properties_returns_them_untouched";
			var customMetadata = new JsonObject {
				["customString"] = "a string",
				["customInt"] = -179,
				["customDouble"] = 1.7,
				["customLong"] = 123123123123123123L,
				["customBool"] = true,
				["customNullable"] = new int?(),
				["customRawJson"] = new JsonObject {
					["subProperty"] = 999
				}
			};

			var metadata = new StreamMetadata(
				maxCount: 17,
				maxAge: TimeSpan.FromSeconds(123321),
				truncateBefore: 23,
				cacheControl: TimeSpan.FromSeconds(7654321),
				acl: new StreamAcl(
					readRole: "readRole",
					writeRole: "writeRole",
					deleteRole: "deleteRole",
					metaWriteRole: "metaWriteRole"),
				customMetadata: JsonDocument.Parse(customMetadata.ToJson()));

			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);
			Assert.AreEqual(17, meta.Metadata.MaxCount);
			Assert.AreEqual(TimeSpan.FromSeconds(123321), meta.Metadata.MaxAge);
			Assert.AreEqual(23, meta.Metadata.TruncateBefore);
			Assert.AreEqual(TimeSpan.FromSeconds(7654321), meta.Metadata.CacheControl);

			Assert.NotNull(meta.Metadata.Acl);
			Assert.AreEqual("readRole", meta.Metadata.Acl.ReadRoles[0]);
			Assert.AreEqual("writeRole", meta.Metadata.Acl.WriteRoles[0]);
			Assert.AreEqual("deleteRole", meta.Metadata.Acl.DeleteRoles[0]);
			//Assert.AreEqual("metaReadRole", meta.StreamMetadata.Acl.MetaReadRole);
			Assert.AreEqual("metaWriteRole", meta.Metadata.Acl.MetaWriteRoles[0]);

			Assert.AreEqual("a string", meta.Metadata.CustomMetadata.RootElement.GetProperty("customString").GetString());
			Assert.AreEqual(-179, meta.Metadata.CustomMetadata.RootElement.GetProperty("customInt").GetInt32());
			Assert.AreEqual(1.7, meta.Metadata.CustomMetadata.RootElement.GetProperty("customDouble").GetDouble());
			Assert.AreEqual(123123123123123123L, meta.Metadata.CustomMetadata.RootElement.GetProperty("customLong").GetInt64());
			Assert.AreEqual(true, meta.Metadata.CustomMetadata.RootElement.GetProperty("customBool").GetBoolean());
			Assert.IsFalse(meta.Metadata.CustomMetadata.RootElement.TryGetProperty("customNullable", out var ignore));
			Assert.AreEqual(@"{""subProperty"":999}", meta.Metadata.CustomMetadata.RootElement.GetProperty("customRawJson").ToJson());
		}

		[Test]
		public async Task setting_structured_metadata_with_multiple_roles_can_be_read_back() {
			const string stream = "setting_structured_metadata_with_multiple_roles_can_be_read_back";

			var metadata = new StreamMetadata(acl: new StreamAcl(
				readRoles: new[] { "r1", "r2", "r3" },
				writeRoles: new[] { "w1", "w2" },
				deleteRoles: new[] { "d1", "d2", "d3", "d4" },
				metaWriteRoles: new[] { "mw1", "mw2" }));

			await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, metadata);

			var meta = await _connection.GetStreamMetadataAsync(stream);
			Assert.AreEqual(stream, meta.StreamName);
			Assert.AreEqual(false, meta.StreamDeleted);
			Assert.AreEqual(0, meta.MetastreamRevision);

			Assert.NotNull(meta.Metadata.Acl);
			Assert.AreEqual(new[] { "r1", "r2", "r3" }, meta.Metadata.Acl.ReadRoles);
			Assert.AreEqual(new[] { "w1", "w2" }, meta.Metadata.Acl.WriteRoles);
			Assert.AreEqual(new[] { "d1", "d2", "d3", "d4" }, meta.Metadata.Acl.DeleteRoles);
			Assert.AreEqual(new[] { "mw1", "mw2" }, meta.Metadata.Acl.MetaWriteRoles);
		}

		// gRPC client doesn't expose a way to provide a JSON representation of a stream metadata.
		// [Test]
		// public async Task setting_correct_metadata_with_multiple_roles_in_acl_allows_to_read_it_as_structured_metadata() {
		// 	const string stream =
		// 		"setting_correct_metadata_with_multiple_roles_in_acl_allows_to_read_it_as_structured_metadata";
  //
		// 	var rawMeta = Helper.UTF8NoBom.GetBytes(@"{
  //                                                          ""$acl"": {
  //                                                              ""$r"": [""r1"", ""r2"", ""r3""],
  //                                                              ""$w"": [""w1"", ""w2""],
  //                                                              ""$d"": [""d1"", ""d2"", ""d3"", ""d4""],
  //                                                              ""$mw"": [""mw1"", ""mw2""],
  //                                                          }
  //                                                     }");
  //
		// 	await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.NoStream, rawMeta);
  //
		// 	var meta = await _connection.GetStreamMetadataAsync(stream);
		// 	Assert.AreEqual(stream, meta.StreamName);
		// 	Assert.AreEqual(false, meta.StreamDeleted);
		// 	Assert.AreEqual(0, meta.MetastreamRevision);
  //
		// 	Assert.NotNull(meta.Metadata.Acl);
		// 	Assert.AreEqual(new[] { "r1", "r2", "r3" }, meta.Metadata.Acl.ReadRoles);
		// 	Assert.AreEqual(new[] { "w1", "w2" }, meta.Metadata.Acl.WriteRoles);
		// 	Assert.AreEqual(new[] { "d1", "d2", "d3", "d4" }, meta.Metadata.Acl.DeleteRoles);
		// 	Assert.AreEqual(new[] { "mw1", "mw2" }, meta.Metadata.Acl.MetaWriteRoles);
		// }
	}
}
