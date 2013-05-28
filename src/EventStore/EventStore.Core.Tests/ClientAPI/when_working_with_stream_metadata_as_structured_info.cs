// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class when_working_with_stream_metadata_as_structured_info : SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;
        private IEventStoreConnection _connection;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _connection = TestConnection.Create(_node.TcpEndPoint);
            _connection.Connect();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        [Test]
        public void setting_empty_metadata_works()
        {
            const string stream = "setting_empty_metadata_works";

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), StreamMetadata.Create());

            var meta = _connection.GetStreamMetadataAsRawBytes(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(Helper.UTF8NoBom.GetBytes("{}"), meta.StreamMetadata);
        }

        [Test]
        public void setting_metadata_few_times_returns_last_metadata_info()
        {
            const string stream = "setting_metadata_few_times_returns_last_metadata_info";
            var metadata = StreamMetadata.Create(17, TimeSpan.FromSeconds(0xDEADBEEF), TimeSpan.FromSeconds(0xABACABA));
            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), metadata);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);

            metadata = StreamMetadata.Create(37, TimeSpan.FromSeconds(0xBEEFDEAD), TimeSpan.FromSeconds(0xDABACABAD));
            _connection.SetStreamMetadata(stream, 0, Guid.NewGuid(), metadata);

            meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(1, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);
        }

        [Test]
        public void trying_to_set_metadata_with_wrong_expected_version_fails()
        {
            const string stream = "trying_to_set_metadata_with_wrong_expected_version_fails";
            Assert.That(() => _connection.SetStreamMetadata(stream, 2, Guid.NewGuid(), StreamMetadata.Create()),
                              Throws.Exception.InstanceOf<AggregateException>()
                              .With.InnerException.InstanceOf<WrongExpectedVersionException>());
        }

        [Test]
        public void setting_metadata_with_expected_version_any_works()
        {
            const string stream = "setting_metadata_with_expected_version_any_works";
            var metadata = StreamMetadata.Create(17, TimeSpan.FromSeconds(0xDEADBEEF), TimeSpan.FromSeconds(0xABACABA));
            _connection.SetStreamMetadata(stream, ExpectedVersion.Any, Guid.NewGuid(), metadata);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);

            metadata = StreamMetadata.Create(37, TimeSpan.FromSeconds(0xBEEFDEAD), TimeSpan.FromSeconds(0xDABACABAD));
            _connection.SetStreamMetadata(stream, ExpectedVersion.Any, Guid.NewGuid(), metadata);

            meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(1, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);
        }

        [Test]
        public void setting_metadata_for_not_existing_stream_works()
        {
            const string stream = "setting_metadata_for_not_existing_stream_works";
            var metadata = StreamMetadata.Create(17, TimeSpan.FromSeconds(0xDEADBEEF), TimeSpan.FromSeconds(0xABACABA));
            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), metadata);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);
        }

        [Test]
        public void setting_metadata_for_existing_stream_works()
        {
            const string stream = "setting_metadata_for_existing_stream_works";

            _connection.AppendToStream(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());

            var metadata = StreamMetadata.Create(17, TimeSpan.FromSeconds(0xDEADBEEF), TimeSpan.FromSeconds(0xABACABA));
            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), metadata);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(metadata.MaxCount, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(metadata.MaxAge, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(metadata.CacheControl, meta.StreamMetadata.CacheControl);
        }

        [Test]
        public void getting_metadata_for_nonexisting_stream_returns_empty_stream_metadata()
        {
            const string stream = "getting_metadata_for_nonexisting_stream_returns_empty_stream_metadata";

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(-1, meta.MetastreamVersion);
            Assert.AreEqual(null, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(null, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(null, meta.StreamMetadata.CacheControl);
        }

        [Test, Ignore("You can't get stream metadata for metastream through ClientAPI")]
        public void getting_metadata_for_metastream_returns_correct_metadata()
        {
            const string stream = "$$getting_metadata_for_metastream_returns_correct_metadata";

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(-1, meta.MetastreamVersion);
            Assert.AreEqual(1, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(null, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(null, meta.StreamMetadata.CacheControl);
        }

        [Test]
        public void getting_metadata_for_deleted_stream_returns_empty_stream_metadata_and_signals_stream_deletion()
        {
            const string stream = "getting_metadata_for_deleted_stream_returns_empty_stream_metadata_and_signals_stream_deletion";

            var metadata = StreamMetadata.Create(17, TimeSpan.FromSeconds(0xDEADBEEF), TimeSpan.FromSeconds(0xABACABA));
            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), metadata);

            _connection.DeleteStream(stream, ExpectedVersion.EmptyStream);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(true, meta.IsStreamDeleted);
            Assert.AreEqual(EventNumber.DeletedStream, meta.MetastreamVersion);
            Assert.AreEqual(null, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(null, meta.StreamMetadata.MaxAge);
            Assert.AreEqual(null, meta.StreamMetadata.CacheControl);
            Assert.AreEqual(null, meta.StreamMetadata.Acl);
        }

        [Test]
        public void setting_correctly_formatted_metadata_as_raw_allows_to_read_it_as_structured_metadata()
        {
            const string stream = "setting_correctly_formatted_metadata_as_raw_allows_to_read_it_as_structured_metadata";

            var rawMeta = Helper.UTF8NoBom.GetBytes(@"{
                                                           ""$maxCount"": 17,
                                                           ""$maxAge"": 123321,
                                                           ""$cacheControl"": 7654321,
                                                           ""$acl"": {
                                                               ""$r"": ""readRole"",
                                                               ""$w"": ""writeRole"",
                                                               ""$mw"": ""metaWriteRole""
                                                           },
                                                           ""customString"": ""a string"",
                                                           ""customInt"": -179,
                                                           ""customDouble"": 1.7,
                                                           ""customLong"": 123123123123123123,
                                                           ""customBool"": true,
                                                           ""customNullable"": null,
                                                           ""customRawJson"": {
                                                               ""subProperty"": 999
                                                           }
                                                      }");

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), rawMeta);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(17, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(TimeSpan.FromSeconds(123321), meta.StreamMetadata.MaxAge);
            Assert.AreEqual(TimeSpan.FromSeconds(7654321), meta.StreamMetadata.CacheControl);
            
            Assert.NotNull(meta.StreamMetadata.Acl);
            Assert.AreEqual("readRole", meta.StreamMetadata.Acl.ReadRole);
            Assert.AreEqual("writeRole", meta.StreamMetadata.Acl.WriteRole);
            // meta role removed to allow reading
//            Assert.AreEqual("metaReadRole", meta.StreamMetadata.Acl.MetaReadRole);
            Assert.AreEqual("metaWriteRole", meta.StreamMetadata.Acl.MetaWriteRole);

            Assert.AreEqual("a string", meta.StreamMetadata.GetValue<string>("customString"));
            Assert.AreEqual(-179, meta.StreamMetadata.GetValue<int>("customInt"));
            Assert.AreEqual(1.7, meta.StreamMetadata.GetValue<double>("customDouble"));
            Assert.AreEqual(123123123123123123L, meta.StreamMetadata.GetValue<long>("customLong"));
            Assert.AreEqual(true, meta.StreamMetadata.GetValue<bool>("customBool"));
            Assert.AreEqual(null, meta.StreamMetadata.GetValue<int?>("customNullable"));
            Assert.AreEqual(@"{""subProperty"":999}", meta.StreamMetadata.GetValueAsRawJsonString("customRawJson"));
        }

        [Test]
        public void setting_structured_metadata_with_custom_properties_returns_them_untouched()
        {
            const string stream = "setting_structured_metadata_with_custom_properties_returns_them_untouched";

            StreamMetadata metadata = StreamMetadata.Build()
                                                    .SetMaxCount(17)
                                                    .SetMaxAge(TimeSpan.FromSeconds(123321))
                                                    .SetCacheControl(TimeSpan.FromSeconds(7654321))
                                                    .SetReadRole("readRole")
                                                    .SetWriteRole("writeRole")
                                                    //.SetMetadataReadRole("metaReadRole")
                                                    .SetMetadataWriteRole("metaWriteRole")
                                                    .SetCustomProperty("customString", "a string")
                                                    .SetCustomProperty("customInt", -179)
                                                    .SetCustomProperty("customDouble", 1.7)
                                                    .SetCustomProperty("customLong", 123123123123123123L)
                                                    .SetCustomProperty("customBool", true)
                                                    .SetCustomProperty("customNullable", new int?())
                                                    .SetCustomPropertyWithValueAsRawJsonString("customRawJson", 
                                                                                               @"{
                                                                                                       ""subProperty"": 999
                                                                                                 }");

            _connection.SetStreamMetadata(stream, ExpectedVersion.EmptyStream, Guid.NewGuid(), metadata);

            var meta = _connection.GetStreamMetadata(stream);
            Assert.AreEqual(stream, meta.Stream);
            Assert.AreEqual(false, meta.IsStreamDeleted);
            Assert.AreEqual(0, meta.MetastreamVersion);
            Assert.AreEqual(17, meta.StreamMetadata.MaxCount);
            Assert.AreEqual(TimeSpan.FromSeconds(123321), meta.StreamMetadata.MaxAge);
            Assert.AreEqual(TimeSpan.FromSeconds(7654321), meta.StreamMetadata.CacheControl);

            Assert.NotNull(meta.StreamMetadata.Acl);
            Assert.AreEqual("readRole", meta.StreamMetadata.Acl.ReadRole);
            Assert.AreEqual("writeRole", meta.StreamMetadata.Acl.WriteRole);
            //Assert.AreEqual("metaReadRole", meta.StreamMetadata.Acl.MetaReadRole);
            Assert.AreEqual("metaWriteRole", meta.StreamMetadata.Acl.MetaWriteRole);
            
            Assert.AreEqual("a string", meta.StreamMetadata.GetValue<string>("customString"));
            Assert.AreEqual(-179, meta.StreamMetadata.GetValue<int>("customInt"));
            Assert.AreEqual(1.7, meta.StreamMetadata.GetValue<double>("customDouble"));
            Assert.AreEqual(123123123123123123L, meta.StreamMetadata.GetValue<long>("customLong"));
            Assert.AreEqual(true, meta.StreamMetadata.GetValue<bool>("customBool"));
            Assert.AreEqual(null, meta.StreamMetadata.GetValue<int?>("customNullable"));
            Assert.AreEqual(@"{""subProperty"":999}", meta.StreamMetadata.GetValueAsRawJsonString("customRawJson"));
        }
    }
}
